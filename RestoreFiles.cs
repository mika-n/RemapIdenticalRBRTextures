using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Runtime.InteropServices;
using System.IO.Compression;
//using System.Diagnostics;
//using System.Reflection.Metadata;

namespace RemapIdenticalRBRTextures
{
    class RestoreFiles
    {

        //-----------------------------------------------------------------------------------------------------------------------
        // Restore RBZ content (ie. with identical textures)
        //
        public static int RestoreIdenticalTextures(string rbrFolder, string backupFolder, List<int> trackIDList, bool forceUpdate, bool zipFast)
        {
            // - Extract all RBZ files to a temp folder
            // - Read TextureFilenameMap1234.ini content into a list
            // - Process the list in END-to-BEGIN order and copy the remapped file back to the original filename also
            // - Repack RBZ files
            string[] allSkyTypes = { "M", "N", "E", "O" };

            string trackIDStr = string.Empty;
            string trackName;
            string trackFolder;
            string sourceFile;

            string textureFilenameMapFileName;

            // List of trackIDs as a string
            foreach (int trackID in trackIDList)
            {
                if (!string.IsNullOrEmpty(trackIDStr))
                {
                    trackIDStr += "_";
                }

                // "320_321_322" trackID string identifier (used in temporary folder and filenames)
                trackIDStr += trackID.ToString();
            }

            backupFolder = Path.Combine(backupFolder + "\\", $"{DateTime.Now.ToString("yyyyMMdd_HHmmss")}_{trackIDStr}");

            // Temporary folder location for RBZ extraction and a temporary texture INI mapfile
            string tempZipFolder = Path.Combine(backupFolder + "\\", "rbz");

            // Lookup the TrackName INI option from Tracks.ini file. M/N/E/O_textures.rbz files are in the trackName folder
            INIFile tracksINIFile = new INIFile(Path.Combine(@rbrFolder, "Maps\\Tracks.ini"));

            //
            // Extract all RBZ files into a temp folder from all tracks specifeid in -track cmdline option (one or more)
            //
            foreach (int trackID in trackIDList)
            {
                trackName = tracksINIFile.ReadValue($"Map{trackID}", "TrackName");

                if (!string.IsNullOrEmpty(trackName))
                {
                    trackFolder = Path.GetDirectoryName(Path.Combine(@rbrFolder, trackName));

                    // TextureFilenameMap1234.ini file doesn't exist then nothing to restore
                    textureFilenameMapFileName = Path.Combine(rbrFolder, trackFolder, "TextureFilenameMap" + trackID.ToString() + ".ini");
                    if (!File.Exists(textureFilenameMapFileName))
                        continue;

                    // Extract all textures.rbz files to a temp folder
                    Directory.CreateDirectory(tempZipFolder);
                    foreach (string skyType in allSkyTypes)
                    {
                        sourceFile = Path.Combine(@rbrFolder, trackName + "_" + skyType + "_textures.rbz");
                        if (File.Exists(sourceFile))
                        {
                            Console.WriteLine($"Unziping {sourceFile} to {tempZipFolder}");
                            ZipFile.ExtractToDirectory(sourceFile, tempZipFolder + "\\", true);
                        }
                    }
                }
            }

            Console.WriteLine("Restoring original texture files");

            List<string> textureFilenameMap = null;
            List<string> delayedTextureFilenameMap = new List<string>();
            bool delayedFileFound = true;

            while (delayedFileFound == true)
            {
                delayedTextureFilenameMap.Clear();
                delayedFileFound = false;

                foreach (int trackID in trackIDList)
                {
                    trackName = tracksINIFile.ReadValue($"Map{trackID}", "TrackName");
                    if (!string.IsNullOrEmpty(trackName))
                    {
                        trackFolder = Path.GetDirectoryName(Path.Combine(@rbrFolder, trackName));

                        // Iterate through TextureFilenameMap1234.ini file in reverse order and restore DDS files
                        textureFilenameMapFileName = Path.Combine(rbrFolder, trackFolder, "TextureFilenameMap" + trackID.ToString() + ".ini");
                        if (!File.Exists(textureFilenameMapFileName))
                            continue;

                        textureFilenameMap = new List<string>(File.ReadAllLines(textureFilenameMapFileName));

                        for (int idx = textureFilenameMap.Count - 1; idx >= 0; idx--)
                        {
                            if (RestoreRemappedTextureFile(tempZipFolder, textureFilenameMap[idx], trackID) == false)
                            {
                                // The target texture file doesn't exist yet. Maybe it was removed as an identical file?
                                // Process this mapping line again after all other lines have been processed
                                delayedTextureFilenameMap.Add(textureFilenameMap[idx]);
                            }
                        }

                        // Process delayed restorations (ie. target files which were remapped also). Repeat this until no more files found.
                        // (the list is processed in FOR loop and iterated in reverse order because the list items are removed while iterating. Reverse-ordered FOR loop makes it possible to remove list items without making a copy of the list)
                        if (delayedTextureFilenameMap.Count <= 0)
                        {
                            delayedFileFound = false;
                        }
                        else
                        {
                            delayedFileFound = true;
                            while (delayedTextureFilenameMap.Count > 0 && delayedFileFound)
                            {
                                delayedFileFound = false;
                                for (int idx = delayedTextureFilenameMap.Count - 1; idx >= 0; idx--)
                                {
                                    if (RestoreRemappedTextureFile(tempZipFolder, delayedTextureFilenameMap[idx], trackID) == true)
                                    {
                                        delayedTextureFilenameMap.RemoveAt(idx);
                                        delayedFileFound = true;
                                    }
                                }
                            }

                            if (delayedTextureFilenameMap.Count > 0 && delayedFileFound == false)
                            {
                                Console.WriteLine($"WARNING {delayedTextureFilenameMap.Count} unprocessed delayed file restorations");
                            }
                        }
                    }
                }
            }

            // Copy new RBZ texture archives to trackFolder
            foreach (int trackID in trackIDList)
            {
                trackName = tracksINIFile.ReadValue($"Map{trackID}", "TrackName");
                if (!string.IsNullOrEmpty(trackName))
                {
                    trackFolder = Path.GetDirectoryName(Path.Combine(@rbrFolder, trackName));

                    textureFilenameMapFileName = Path.Combine(trackFolder, "TextureFilenameMap" + trackID.ToString() + ".ini");
                    if (!File.Exists(textureFilenameMapFileName))
                        continue;

                    RepackTexturesAndCopyToRBRMapsFolder(tempZipFolder, rbrFolder, backupFolder, trackID, trackFolder, forceUpdate, zipFast);
                }
            }

            if (Directory.Exists(tempZipFolder))
                Directory.Delete(tempZipFolder, true);

            return 0;
        }


        //--------------------------------------------------------------------------------------------------
        // Restores remapped target texture file back to source texture file. Returns TRUE if the mapping text line was processed
        // and FALSE if the target file didn't exist yet and should be processed again after all other lines
        private static bool RestoreRemappedTextureFile(string tempZipFolder, string textureMappingTextLine, int trackID)
        {
            // Skip empty and comment lines
            if (string.IsNullOrEmpty(textureMappingTextLine) || textureMappingTextLine.Trim().Length <= 1 || textureMappingTextLine.TrimStart()[0] == ';')
                return true;

            // Skip lines without TAB separator
            string[] splittedStr = textureMappingTextLine.Trim().Split('\t');
            if (splittedStr.Length < 2)
                return true;

            // Skip lines with empty texture filenames
            string sourceTextureFilename = splittedStr[0].Trim();
            string targetTextureFilename = splittedStr[1].Trim();
            if (string.IsNullOrEmpty(sourceTextureFilename) || string.IsNullOrEmpty(targetTextureFilename))
                return true;

            if (!targetTextureFilename.StartsWith("track-" + trackID.ToString() + "_", StringComparison.InvariantCultureIgnoreCase))
            {
                // Target was missing "track-320_m_textures" prefix. Copy it from the sourceTextureFilename string
                int iPathSepPos = sourceTextureFilename.IndexOf('\\');
                if (iPathSepPos <= 0)
                {
                    iPathSepPos = sourceTextureFilename.IndexOf('/');
                    if (iPathSepPos <= 0)
                        iPathSepPos = -1;
                }

                targetTextureFilename = sourceTextureFilename.Substring(0, iPathSepPos + 1) + targetTextureFilename;
            }

            // Copy target texture as a source texture filename to restore the original DDS file
            sourceTextureFilename = Path.Combine(tempZipFolder, sourceTextureFilename);
            targetTextureFilename = Path.Combine(tempZipFolder, targetTextureFilename);

            if (File.Exists(sourceTextureFilename))
            {
                // File is already restored
                return true;
            }
            else if (!File.Exists(targetTextureFilename))
            {
                // The file doesn't exist yet. Process it later on after all other mapping lines have been processed
                // because could be the target file was also remapped
                return false;
            }
            else
            {
                // Target texture is the "target filename in TextureFileNamemap.ini" file, so when we want to
                // restore original files we copy the remapped target back to remapped source file.
                //Console.WriteLine($"Copying {targetTextureFilename} to {sourceTextureFilename}");
                File.Copy(targetTextureFilename, sourceTextureFilename);
                return true;
            }
        }


        //-------------------------------------------------------------------
        // Repack textures and copy back to RBR Maps folder
        //
        private static void RepackTexturesAndCopyToRBRMapsFolder(string tempZipFolder, string rbrFolder, string backupFolder, int trackID, string trackFolder, bool forceUpdate, bool zipFast)
        {
            int idx = 0;

            string rbzOrigFileName;
            string rbzArchiveFileName;
            string tempZipFile;

            DirectoryInfo[] dirs = (new DirectoryInfo(tempZipFolder)).GetDirectories($"track-{trackID}_*");

            foreach (var rbzDir in dirs)
            {
                // Texture folders should have a syntax or track-123_M_textures.rbz. The path prefix is the name of the RBZ archive file
                rbzArchiveFileName = rbzDir.Name + ".rbz";
                tempZipFile = Path.Combine(backupFolder + "\\", "new_" + rbzArchiveFileName);

                if (Path.GetExtension(rbzArchiveFileName) == ".rbz")
                {
                    idx++;
                    Console.WriteLine($"Ziping {idx}/{dirs.Length} rbz {rbzArchiveFileName}");

                    // TODO: 7zip tool if it makes smaller zip files?
                    ZipFile.CreateFromDirectory(rbzDir.FullName, tempZipFile, (zipFast ? CompressionLevel.NoCompression : CompressionLevel.SmallestSize), true);
                }
            }

            if (forceUpdate)
            {
                string textureFilenameMapFileName = Path.Combine(trackFolder, "TextureFilenameMap" + trackID.ToString() + ".ini");

                // Backup the original TextureFilenameMap1234.ini file
                File.Copy(textureFilenameMapFileName, Path.Combine(backupFolder, "TextureFilenameMap" + trackID.ToString() + ".ini"));

                // Backup original RBZ archive files
                foreach (var rbzDir in dirs)
                {
                    idx++;
                    rbzArchiveFileName = rbzDir.Name + ".rbz";
                    if (Path.GetExtension(rbzArchiveFileName) == ".rbz")
                    {
                        rbzOrigFileName = Path.Combine(trackFolder + "\\", rbzArchiveFileName);

                        Console.WriteLine($"Backuping {rbzOrigFileName} to {backupFolder}");
                        File.Copy(rbzOrigFileName, Path.Combine(backupFolder + "\\", rbzArchiveFileName));
                    }
                }

                // Copy all new_*.rbz files back into the RBR map folder path
                foreach (var rbzDir in dirs)
                {
                    rbzArchiveFileName = rbzDir.Name + ".rbz";
                    tempZipFile = Path.Combine(backupFolder + "\\", "new_" + rbzArchiveFileName);
                    if (File.Exists(tempZipFile) && Path.GetExtension(rbzArchiveFileName) == ".rbz")
                    {
                        rbzOrigFileName = Path.Combine(trackFolder + "\\", rbzArchiveFileName);

                        Console.WriteLine($"Updating {rbzOrigFileName}");
                        File.Delete(rbzOrigFileName);
                        File.Copy(tempZipFile, Path.Combine(trackFolder + "\\", rbzOrigFileName));
                        File.Delete(tempZipFile);
                    }
                }

                // Remove the now unnecessary TextureFilenameMap1234.ini file (rbz files are now in the original non-deduplicated format)
                File.Delete(textureFilenameMapFileName);
            }
        }
    }

}
