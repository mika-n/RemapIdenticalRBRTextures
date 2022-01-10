// RemapIdenticalRBRTextures
//
// This tool identifies identical DDS texture files in RICHARD BURNS RALLY textures.rbz archive files, removes duplicates and re-packs the RBZ file.
//
// What are the benefits of doing all this for RBR maps?
// - Smaller map pack size in bytes (the size of track-1234_M/N/E/O_textures.rbz files is reduced when there are no duplicated files). 
// - Smaller map pack saves download bandwidth and diskspace in PC.
// - Faster map loading because RBR doesn't re-load a texture if an identical copy is already loaded, plus RBR can read smaller textures.rbz files a bit faster (couple nano seconds faster in SSD systems, 5 nano seconds in historical HDD systems).
// - Wallaby and BlenderRBRExporter doesn't identify identical files yet, so this tool fills the gap and works with all maps created with any tool (even for old maps released years ago).
//
// How this works?
// - Extracts map specific textures.rbz files to a temporary folder
// - Identifies duplicated copies of DDS file, removes duplicated copies and leaves just one copy of the DDS texture file.
// - Creates TextureFilenameMap<MapID>.ini file with links between a removed DDS file and the one preserved copy of the identical file.
// - RallySimFans (RSF) plugin uses the TextureFilenameMap<MapID>.ini file to instruct RBR to use a remapped texture instead of the delete duplicated copy.
//
// Note! Identical file here means that the file content is one-on-one identical in a binary level (not a filename match)
// 
// Shows usage info:
//   RemapIdenticalRBRTextures.exe   
//
// Identifies duplicated textures in track 972 map pack (Maps\Tracks.ini [Map972] map), but does NOT update the map pack.
// This command can be used to "simulate" the process and see the end result in c:\backup\rbr folder without actually modifying anything.
//   RemapIdenticalRBRTextures.exe -rbrFolder "c:\games\Richard Burns Rally" -track 972 -backupFolder c:\backup\rbr
//
// Identifies duplicated textures in track 972 map pack, deletes duplicates and modifies the original RBZ map pack (-DeleteDuplicates option)
//   RemapIdenticalRBRTextures.exe -rbrFolder "c:\games\Richard Burns Rally" -track 972 -backupFolder c:\backup\rbr -DeleteDuplicates
//   
// Copyright (c) 2021-2022 MIKA-N. All rights reserved. This is a free tool for all RBR map authors, but not for commercial use without a permission from the author. Use at your own risk.
// RallySimFans - www.rallysimfans.hu
//
using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Runtime.InteropServices;
using System.IO.Compression;

namespace RemapIdenticalRBRTextures
{
    //
    // INI file helper class
    //
    public class INIFile
    {
        private readonly string _INIFileName;       // Full path filename
        private readonly string _INIFileNamePart;   // Filename part only

        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);
        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        public enum BoolValueType { TrueFalse, OneZero, YesNo }

        private readonly bool _FileExists;
        public bool FileExists { get => _FileExists; }

        public INIFile()
        {
            // nothing to see here
        }

        public INIFile(string iniFileName)
        {
            this._INIFileName = iniFileName;
            this._INIFileNamePart = Path.GetFileNameWithoutExtension(iniFileName);
            this._FileExists = File.Exists(iniFileName);
        }

        public void WriteValue(string Section, string Key, string Value)
        {
            if (string.Compare(this.ReadValue(Section, Key, ""), Value, true) != 0)
                WritePrivateProfileString(Section, Key, Value, this._INIFileName);
        }

        public void WriteValueInt(string Section, string Key, int Value)
        {
            this.WriteValue(Section, Key, Value.ToString());
        }

        public void WriteValueFloat(string Section, string Key, float Value)
        {
            CultureInfo cultureUS = new CultureInfo("en-US");
            if (cultureUS.NumberFormat.NumberDecimalSeparator != ".")
                cultureUS.NumberFormat.NumberDecimalSeparator = ".";

            this.WriteValue(Section, Key, String.Format(cultureUS, "{0:0.######}", Value));
        }

        public void WriteValueBool(string Section, string Key, bool Value, BoolValueType boolValueType = BoolValueType.TrueFalse)
        {
            string strValue;
            if (boolValueType == BoolValueType.OneZero)
                strValue = (Value ? 1 : 0).ToString();
            else if (boolValueType == BoolValueType.YesNo)
                strValue = (Value ? "yes" : "no");
            else
                strValue = (Value ? "true" : "false");

            this.WriteValue(Section, Key, strValue);
        }

        public string ReadValue(string Section, string Key, string defaultValue = "")
        {
            if (!this.FileExists)
                return defaultValue;

            StringBuilder strBuffer = new StringBuilder(256);
            int i = GetPrivateProfileString(Section, Key, defaultValue, strBuffer, 256 - 2, this._INIFileName);

            // Trim and remove enclosing "xxx" double quotes and line end comments
            string resultText = strBuffer.ToString().Trim();

            int commentPos = resultText.IndexOf(';');
            if (commentPos >= 0)
            {
                if (commentPos == 0)
                    resultText = string.Empty;
                else
                    resultText = (resultText[0..(commentPos - 1)]).Trim();
            }

            if (resultText.Length >= 2)
            {
                if (resultText.Length >= 3 && resultText[0] == '"' && resultText[^1] == '"')
                    resultText = resultText[1..^1];
                else
                {
                    if (resultText[resultText.Length - 1] == '"')
                        resultText = resultText.Remove(resultText.Length - 1, 1);

                    if (resultText[0] == '"')
                    {
                        if (resultText.Length >= 2)
                            resultText = resultText[1..];
                        else
                            resultText = string.Empty;
                    }
                }

                resultText = resultText.Trim();
            }

            if (string.IsNullOrEmpty(resultText))
                resultText = defaultValue;

            return resultText;
        }

        public int ReadValueInt(string Section, string Key, int defaultValue = 0)
        {
            string resultText = this.ReadValue(Section, Key, "");
            if (string.IsNullOrEmpty(resultText))
                return defaultValue;
            else
                return int.Parse(resultText);
        }

        public long ReadValueLong(string Section, string Key, long defaultValue = 0)
        {
            string resultText = this.ReadValue(Section, Key, "");
            if (string.IsNullOrEmpty(resultText))
                return defaultValue;
            else
                return long.Parse(resultText);
        }

        public bool ReadValueBool(string Section, string Key, bool defaultValue = false)
        {
            string resultText = this.ReadValue(Section, Key, "");
            if (string.IsNullOrEmpty(resultText))
                return defaultValue;
            else if (string.Compare(resultText, "false", true) == 0 || resultText == "0" || string.Compare(resultText, "no", true) == 0)
                return false;
            else if (string.Compare(resultText, "true", true) == 0 || resultText == "1" || string.Compare(resultText, "yes", true) == 0)
                return true;
            else
                return defaultValue;
        }

        public float ReadValueFloat(string Section, string Key, float defaultValue = 0)
        {
            string resultText = this.ReadValue(Section, Key, "");
            if (string.IsNullOrEmpty(resultText))
                return defaultValue;
            else
            {
                CultureInfo cultureUS = new CultureInfo("en-US");
                if (cultureUS.NumberFormat.NumberDecimalSeparator != ".")
                    cultureUS.NumberFormat.NumberDecimalSeparator = ".";

                return float.Parse(resultText, cultureUS.NumberFormat); // Make sure the decimal separator is "."
            }
        }
    }


    //-----------------------------------------------------
    // RBZ file details (file path, file size, content for duplicate checks)
    //
    public class RBZFileInfo
    {
        private FileInfo fileInfo;

        private bool headerFingerprintProcessed;    // Header fingerprint already read
        private int headerFingerprintSize;          // Size of the fingerprint in bytes (could be less than sizeof headerFingerprint array if the total filesize is less)

        private byte[] headerFingerprint;           // Content of the file header fingerprint
        public byte[] HeaderFingerprint { get { return headerFingerprint; } }

        private byte[] fileContent;                 // Whole file content if the header was identical and we have to compare the whole file

        public bool Processed { get; set; }         // If file has been processed then no need to re-check it (it was already found to be identical or no identical files found)

        private long fileSize;
        public long FileSize 
        {  
            get 
            {
                if (fileSize < 0) fileSize = fileInfo.Length;
                return fileSize;
            } 
        }

        public string FileFullName { get { return fileInfo.FullName; } }

        public void ReleaseContentBuffers()
        {
            // Release byte arrays because this file has been processed and no need to re-check it
            this.headerFingerprint = null;
            this.fileContent = null;
        }

        public int GetHeaderFingerprintSize()
        {
            if(!headerFingerprintProcessed || headerFingerprint == null)
            {
                headerFingerprintProcessed = true;
                headerFingerprint = new byte[4096];
                using (var fs = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    headerFingerprintSize = fs.Read(HeaderFingerprint, 0, HeaderFingerprint.GetUpperBound(0)+1);  
                }
            }

            return headerFingerprintSize;
        }


        // Are files identical? Could use hashKey here, but do the comparing using brute-force byte array comparing (optimized using cached header buffer because often files differ already during the first block)
        public bool IsFileIdentical(RBZFileInfo otherFile)
        {
            if(this.IsHeaderFingerprintIdentical(otherFile))
            {
                // If headers are identical then check rest of the file also to be sure files really are identical.
                // But usually if files were different then it is spotted already in the header checks, so non-identical files very rarely comes to this slower block
                if(this.fileContent == null)
                    this.fileContent = File.ReadAllBytes(fileInfo.FullName);

                if (otherFile.fileContent == null)
                    otherFile.fileContent = File.ReadAllBytes(otherFile.fileInfo.FullName);

                int thisFileSize = this.fileContent.GetUpperBound(0) + 1;
                int otherFileSize = otherFile.fileContent.GetUpperBound(0) + 1;
                if (thisFileSize == otherFileSize)
                {
                    for (int idx = this.GetHeaderFingerprintSize(); idx < thisFileSize; idx++)
                    {
                        if (this.fileContent[idx] != otherFile.fileContent[idx])
                            return false;
                    }

                    return true;
                }
                return false;
            }

            return false;
        }

        // Are headers of two files identical? The content of the header is cached in-memory and readin only once per file to speed up initial comparison
        private bool IsHeaderFingerprintIdentical(RBZFileInfo otherFile)
        {
            int idx;
            int thisHeaderSize = this.GetHeaderFingerprintSize();
            int otherHeaderSize = otherFile.GetHeaderFingerprintSize();

            if (thisHeaderSize == otherHeaderSize)
            {
                if (thisHeaderSize > 128)
                {
                    // Many DDS textures may have identical header in the first 128 block, so start comparing after the initial block if the fileSize>128 bytes
                    for (idx = 128; idx < thisHeaderSize; idx++)
                    {
                        if (this.headerFingerprint[idx] != otherFile.headerFingerprint[idx])
                            return false;
                    }
                }

                // Compare the lower 128 block if >128 block was identical
                for (idx = 0; idx < 128 && idx < thisHeaderSize; idx++)
                {
                    if (this.headerFingerprint[idx] != otherFile.headerFingerprint[idx])
                        return false;
                }

                return true;
            }
            return false;
        }

        public RBZFileInfo(FileInfo fileInfo)
        {
            this.Processed = false;
            this.fileInfo = fileInfo;
            this.fileSize = -1;

            this.fileContent = null;
            this.headerFingerprint = null;
            this.headerFingerprintProcessed = false;
            this.headerFingerprintSize = 0;
        }
    }


    //-----------------------------------------------------
    class Program
    {
        static void Main(string[] args)
        {
            int retValue = 0;

            bool forceUpdate = false;
            bool deleteDuplicatedFiles = false;
            string rbrFolder = string.Empty;
            string backupFolder = string.Empty;
            int trackID = -1;

            if (args.Length <= 0)
            {
                Console.WriteLine("RallySimFans - Remap Identical RBR Textures. Tool for Richard Burns Rally to re-pack RBZ texture files by removing identical texture files. Duplicated are remapped to one common file by creating TextureFilenameMap<trackID>.ini list");
                Console.WriteLine("");
                Console.WriteLine("Copyright (c) 2021-2022, MIKA-N and RallySimFans team at www.rallysimfans.hu. All RBR map authors can use this tool for free. Use at your own risk. Not for commercial use without a permission from the author.");
                Console.WriteLine("");
                Console.WriteLine("Usage: Remap files, but does NOT delete duplicated files from textures.rbz");
                Console.WriteLine("   RemapIdenticalRBRTextures.exe -rbrFolder \"c:\\games\\Richard Burns Rally\" -track 972 -backupFolder c:\\backup\\rbr");
                Console.WriteLine("");
                Console.WriteLine("Usage: Remap files and DELETES duplicated files from textures.rbz");
                Console.WriteLine("   RemapIdenticalRBRTextures.exe -rbrFolder c:\\games\\rsf -track 972 -backupFolder c:\\backup\\rbr -DeleteDuplicates -ForceUpdate");

                System.Environment.Exit(1);
            }

            // Parse cmdline options
            for (int idx = 0; idx < args.Length; idx++)
            {
                if (string.Compare(args[idx], "-deleteduplicates", true) == 0)
                    deleteDuplicatedFiles = true;
                else if (string.Compare(args[idx], "-forceupdate", true) == 0)
                    forceUpdate = true;
                else if (string.Compare(args[idx], "-rbrfolder", true) == 0 && args.Length > idx + 1)
                    rbrFolder = args[++idx];
                else if (string.Compare(args[idx], "-track", true) == 0 && args.Length > idx + 1)
                    trackID = int.Parse(args[++idx]);
                else if (string.Compare(args[idx], "-backupfolder", true) == 0 && args.Length > idx + 1)
                    backupFolder = args[++idx];
            }

            if (string.IsNullOrEmpty(backupFolder))
            {
                Console.Write("ERROR. Missing -backupFolder cmdline option");
                retValue = 1;
            }
            else if (trackID > 0)
            {
                string[] allSkyTypes = { "M", "N", "E", "O", "S" };

                string tracksFile;
                string trackName;
                string trackFolder;
                string stageName;
                string sourceFile;
                string textureFilenameMapFile;

                SortedDictionary<string, string> textureRemapList = new SortedDictionary<string, string>(); // Name=Value pair of remapping (value=remap source, key=remap target)

                int dupNumOfFiles;

                // Lookup the TrackName INI option from Tracks.ini file. M/N/E/O/S_textures.rbz files are in the trackName folder
                tracksFile = Path.Combine(@rbrFolder, "Maps\\Tracks.ini");
                INIFile tracksINIFile = new INIFile(tracksFile);
                trackName = tracksINIFile.ReadValue($"Map{trackID}", "TrackName");
                stageName = tracksINIFile.ReadValue($"Map{trackID}", "StageName");

                if (!string.IsNullOrEmpty(trackName))
                {
                    backupFolder = Path.Combine(backupFolder + "\\", $"{DateTime.Now.ToString("yyyyMMdd_HHmmss")}_{trackID}");
                    trackFolder = Path.GetDirectoryName(Path.Combine(@rbrFolder, trackName));
                    textureFilenameMapFile = Path.Combine(trackFolder + "\\", $"TextureFilenameMap{trackID}.ini");

                    // Temporary folder for RBZ extraction and a temporary texture INI mapfile
                    string tempZipFolder = Path.Combine(backupFolder + "\\", "rbz");
                    string tempTextureFilenameMapFile = Path.Combine(backupFolder + "\\", $"TextureFilenameMap{trackID}.ini");

                    Console.WriteLine($"Checking duplicated DDS in {stageName} {Path.Combine(@rbrFolder, trackName + "_M/N/E/O/S_textures.rbz")}");
                    Console.WriteLine($"RSF RBR remap file {textureFilenameMapFile}");

                    // Extract all textures.rbz files to a temp folder
                    Directory.CreateDirectory(tempZipFolder);
                    foreach (string skyType in allSkyTypes)
                    {
                        sourceFile = Path.Combine(@rbrFolder, trackName + "_" + skyType + "_textures.rbz");
                        if (File.Exists(sourceFile))
                        {
                            Console.WriteLine($"Unziping {sourceFile} to {tempZipFolder}");
                            ZipFile.ExtractToDirectory(sourceFile, Path.Combine(tempZipFolder + "\\", Path.GetFileName(sourceFile)), true);
                        }
                    }


                    // Check identical DDS files (the file content) and remove duplicated copies
                    dupNumOfFiles = RSFCheckDuplicatedFiles(rbrFolder, trackFolder, tempZipFolder, textureRemapList, deleteDuplicatedFiles);
                    Console.WriteLine($"{dupNumOfFiles} DDS texture duplicates found");

                    if(dupNumOfFiles > 0)
                    {
                        // Write the new TextureFilenameMap.ini file at first in temp folder before copying the file to RBR maps folder
                        using (StreamWriter sw = new StreamWriter(tempTextureFilenameMapFile))
                        {
                            sw.WriteLine($"; {DateTime.Now.ToString("yyyyMMdd_HHmmss")} - {stageName} - Autogenerated remap file by RallySimFans RemapIdenticalRBRTextures tool");
                            sw.WriteLine("; TAB char is the separator (originalFile remapTargetFile)");

                            foreach (var item in textureRemapList)
                            {
                                sw.WriteLine($"{item.Key.ToLower()}\t{item.Value.ToLower()}");
                            }
                        }


                        // Copy new RBZ texture archives to trackFolder
                        RepackTexturesAndCopyToRBRMapsFolder(tempZipFolder, tempTextureFilenameMapFile, backupFolder, trackFolder, deleteDuplicatedFiles, forceUpdate);
                    }

                    if(Directory.Exists(tempZipFolder))
                        Directory.Delete(tempZipFolder, true);
                }
                else
                {
                    Console.Write($"ERROR. {tracksFile} missing or unknown track {trackID}. Cannot find textures.rbz files without [Map{trackID}] definition");
                    retValue = 1;
                }
            }
            else
            {
                Console.Write("ERROR. Invalid or missing cmdline options. Run RemapIdenticalRBRTextures.exe to see all options.");
                retValue = 1;
            }

            System.Environment.Exit(retValue);
        }


        //-------------------------------------------------------------------
        // Check if there are identical texture files within RBR textures.rbz file. Create TextureFilenameMapXXX.ini remap file and optionally delete duplicated files in the RBZ archive to save diskspace.
        // Return number of duplicated files found or 0 if no duplicates</returns>
        //
        private static int RSFCheckDuplicatedFiles(string rbrFolder, string trackFolder, string tempZipFolder, SortedDictionary<string, string> textureRemapList, bool deleteDuplicatedFiles)
        {
            int numOfTotalFiles;
            int numOfDuplicateFiles = 0;

            SortedDictionary<string, RBZFileInfo> fileList = new SortedDictionary<string,RBZFileInfo>();
            Dictionary<string, RBZFileInfo> sameSizeList = new Dictionary<string, RBZFileInfo>();

            Console.WriteLine($"Checking duplicates");

            // Recursively iterate all files from top to bottom folder (filename+filesize)
            DirectoryInfo dir = new DirectoryInfo(tempZipFolder);
            numOfTotalFiles = GetFilesRecursively(dir, "", fileList);

            Console.WriteLine($"{numOfTotalFiles} DDS texture files");

            //
            // Check all files for duplicates. Remove duplicates and add a new TextureFilenameMap.ini remapping entry
            // - Get the original fileName+fileInfo
            // - Find all files with the same size (skip the original fileName)
            // - Compare the file content between the original and all files with the same size (fileName doesn't have to be the same)
            // - Add duplicate files as TextureFilenameMap.ini remap lines and leave just the first (original) file
            //
            foreach(var origItem in fileList)
            {
                if(!origItem.Value.Processed)
                {
                    //Console.WriteLine($"{origItem.Key} = {origItem.Value.FileSize}");
                    origItem.Value.Processed = true;

                    // Find all files with the same filesize. Ignore small files (no need to re-link those because space saving would be minimal anyway)
                    sameSizeList.Clear();
                    if (origItem.Value.FileSize >= 1024)
                    {
                        foreach (var dupItem in fileList)
                        {
                            if (!dupItem.Value.Processed && dupItem.Value.FileSize == origItem.Value.FileSize)
                                sameSizeList.Add(dupItem.Key, dupItem.Value);
                        }
                    }

                    //Console.WriteLine($"{origItem.Key} has {sameSizeList.Count} potentially duplicated files. FileSize {origItem.Value.FileSize}");

                    // Compare origItem and all other files with the same size and remove duplicated identical files and add a new TextureFilenameMap.ini line
                    foreach (var sameSizeItem in sameSizeList)
                    {
                        if (origItem.Value.IsFileIdentical(sameSizeItem.Value))
                        {
                            //Console.WriteLine($"{sameSizeItem.Key} = {origItem.Key}");

                            sameSizeItem.Value.Processed = true;
                            sameSizeItem.Value.ReleaseContentBuffers();

                            // Delete duplicate copy and add a remap definition to the original identical file
                            File.Delete(sameSizeItem.Value.FileFullName);
                            textureRemapList.Add(GetTextureRemapFileNameSource(sameSizeItem.Key), GetTextureRemapFileNameTarget(origItem.Key, sameSizeItem.Key));

                            numOfDuplicateFiles++;                            
                        }
                    }

                    origItem.Value.ReleaseContentBuffers();
                }
            }

            return numOfDuplicateFiles;
        }


        //-------------------------------------------------------------------
        // Repack textures and copy back to RBR Maps folder
        //
        private static void RepackTexturesAndCopyToRBRMapsFolder(string tempZipFolder, string tempTextureFilenameMapFile, string backupFolder, string trackFolder, bool deleteDuplicatedFiles, bool forceUpdate)
        {
            int idx = 0;

            FileInfo sourceRBZFileInfo;
            FileInfo tmpZipFileFileInfo;
            string rbzOrigFileName;
            string rbzArchiveFileName;
            string tempZipFile;

            double newTotalSize = 0;
            double oldTotalSize = 0;

            DirectoryInfo[] dirs = (new DirectoryInfo(tempZipFolder)).GetDirectories();

            foreach (var rbzDir in dirs)
            {
                // Texture folders should have a syntax or track-123_M_textures.rbz. The path prefix is the name of the RBZ archive file
                rbzArchiveFileName = rbzDir.Name;
                tempZipFile = Path.Combine(backupFolder + "\\", "new_" + rbzArchiveFileName);

                if (Path.GetExtension(rbzArchiveFileName) == ".rbz")
                {
                    idx++;
                    Console.WriteLine($"Ziping {idx}/{dirs.Length} rbz {rbzArchiveFileName}");
                    ZipFile.CreateFromDirectory(rbzDir.FullName, tempZipFile, CompressionLevel.Optimal, false);
                }
            }

            // Temporary RBZ unzip folder is no longer needed when all new_*.rbz archive files have been created
            Directory.Delete(tempZipFolder, true);

            // Backup the original RBZ archive file if the new RBZ is smaller than the original one
            foreach (var rbzDir in dirs)
            {
                rbzArchiveFileName = rbzDir.Name;
                tempZipFile = Path.Combine(backupFolder + "\\", "new_" + rbzArchiveFileName);
                if (Path.GetExtension(rbzArchiveFileName) == ".rbz")
                {
                    rbzOrigFileName = Path.Combine(trackFolder + "\\", rbzArchiveFileName);

                    sourceRBZFileInfo = new FileInfo(rbzOrigFileName);
                    tmpZipFileFileInfo = new FileInfo(tempZipFile);
                    if (sourceRBZFileInfo.Length > tmpZipFileFileInfo.Length)
                    {
                        double newSizeMB = tmpZipFileFileInfo.Length / (1024.0 * 1024.0);
                        double oldSizeMB = sourceRBZFileInfo.Length / (1024.0 * 1024.0);

                        newTotalSize += tmpZipFileFileInfo.Length;
                        oldTotalSize += sourceRBZFileInfo.Length;

                        if (deleteDuplicatedFiles)
                        {
                            Console.WriteLine($"Backuping {rbzOrigFileName} to {backupFolder}");
                            File.Copy(rbzOrigFileName, Path.Combine(backupFolder + "\\", rbzArchiveFileName));
                        }

                        Console.WriteLine($"New size {newSizeMB.ToString("N2")} MB < {oldSizeMB.ToString("N2")} MB  (Saving {(oldSizeMB - newSizeMB).ToString("N2")} MBs)");
                    }
                    else
                    {
                        Console.WriteLine($"The new {rbzArchiveFileName} would not save space. Keeping the original file");
                        File.Delete(tempZipFile);
                    }
                }
            }

            Console.WriteLine($"Total space saving {((oldTotalSize / (1024.0*1024.0)) - (newTotalSize / (1024.0*1024.0))).ToString("N2")} MBs");

            // Copy TextureFilenameMapXXX.ini file to trackFolder
            if (deleteDuplicatedFiles)
            {
                string textureMapFileNamePart = Path.GetFileName(tempTextureFilenameMapFile);
                string targetTextureMapFileName = Path.Combine(trackFolder + "\\", textureMapFileNamePart);

                if(forceUpdate == false && File.Exists(targetTextureMapFileName))
                {
                    // Hmmm... Existing TextureFilenameMapXXX.ini file. There may be conflicts if already repacked and remapped files are remapped again
                    Console.WriteLine($"ERROR. {targetTextureMapFileName} already exists. Use -ForceUpdate option to force update RBZ files");
                    return;
                }

                Console.WriteLine($"Copying {textureMapFileNamePart} to {trackFolder}");
                File.Copy(tempTextureFilenameMapFile, targetTextureMapFileName, true);
            }

            // Copy all new_*.rbz files back to RBR path to update rbz archive file with smaller rbz files
            foreach (var rbzDir in dirs)
            {
                rbzArchiveFileName = rbzDir.Name;
                tempZipFile = Path.Combine(backupFolder + "\\", "new_" + rbzArchiveFileName);
                if (File.Exists(tempZipFile) && Path.GetExtension(rbzArchiveFileName) == ".rbz")
                {
                    rbzOrigFileName = Path.Combine(trackFolder + "\\", rbzArchiveFileName);

                    if (deleteDuplicatedFiles)
                    {
                        Console.WriteLine($"Updating {rbzOrigFileName}");
                        File.Delete(rbzOrigFileName);
                        File.Copy(tempZipFile, Path.Combine(trackFolder + "\\", rbzOrigFileName));
                    }
                    else
                    {
                        Console.WriteLine($"DeleteDuplicates option NOT set. Skipping file update {rbzOrigFileName}");
                    }
                }
            }
        }


        // Remove RBZ archive fileName prefix from the texture remap name (track_123_E_textures.rbz\track_123_E_textures\dry\normal\cone.dds -> track_123_E_textures\dry\normal\cone.dds)
        private static string GetTextureRemapFileNameSource(string rbzTextureFileName)
        {
            int pathSepPos = rbzTextureFileName.IndexOf('\\');
            if (pathSepPos > 0)
                return rbzTextureFileName.Substring(pathSepPos + 1);

            return rbzTextureFileName;
        }


        // Remove RBZ archive fileName prefix from the target name AND the first texture path if it is the same as source name
        private static string GetTextureRemapFileNameTarget(string rbzTextureFileNameTarget, string rbzTextureFileNameSource)
        {
            rbzTextureFileNameSource = GetTextureRemapFileNameSource(rbzTextureFileNameSource);
            rbzTextureFileNameTarget = GetTextureRemapFileNameSource(rbzTextureFileNameTarget);

            int pathSepPosSource = rbzTextureFileNameSource.IndexOf('\\');
            int pathSepPosTarget = rbzTextureFileNameTarget.IndexOf('\\');
            if (pathSepPosSource > 0 && pathSepPosSource == pathSepPosTarget)
            {
                if (rbzTextureFileNameSource.Substring(0, pathSepPosSource) == rbzTextureFileNameTarget.Substring(0, pathSepPosTarget))
                    return rbzTextureFileNameTarget.Substring(pathSepPosTarget + 1);
            }

            return rbzTextureFileNameTarget;
        }


        //----------------------------------------------------------------------------------------------
        // Recursively collect file info from all files under dirInfo directory
        //
        private static int GetFilesRecursively(DirectoryInfo dir, string dirPath, SortedDictionary<string, RBZFileInfo> fileList)
        {
            int numOfFiles = 0;

            // Add files from the current dir
            FileInfo[] files = dir.GetFiles();
            foreach (var fileInfo in files)
            {
                if (string.Compare(fileInfo.Extension, ".dds", true) == 0)
                {
                    fileList.Add(dirPath + fileInfo.Name, new RBZFileInfo(fileInfo));
                    numOfFiles++;
                }
            }

            // Get all child directories and collect file infos recursively
            DirectoryInfo[] dirs = dir.GetDirectories();
            foreach (var childDir in dirs)
            {
                numOfFiles += GetFilesRecursively(childDir, dirPath + childDir.Name + "\\",fileList);
            }

            return numOfFiles;
        }
    }
}
