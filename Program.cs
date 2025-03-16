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
//using System.Globalization;
using System.Text;
//using System.Runtime.InteropServices;
using System.IO.Compression;
//using System.Diagnostics;
//using System.Reflection.Metadata;

namespace RemapIdenticalRBRTextures
{
    //-----------------------------------------------------
    class Program
    {
        static void Main(string[] args)
        {
            int retValue = 0;

            bool zipFast = false;                  // TRUE=Zip files using "store only" to speed up the process
            bool forceUpdate = false;              // TRUE=Force update TextuteFilenameMap1234.ini file even when it already exists            
            bool deleteDuplicatedFiles = false;    // TRUE=Delete files and update the original RBZ archive file, FALSE=simulate and calculate space saving
            bool restoreOriginalFiles = false;     // TRUE=Revert the deduplication and get back to the original RBZ file content

            string rbrFolder = string.Empty;
            string backupFolder = string.Empty;

            List<int> trackIDList = new List<int>();

            if (args.Length <= 0)
            {
                Console.WriteLine("RallySimFans - Remap Identical RBR Textures. Tool for Richard Burns Rally to re-pack RBZ texture files by removing identical texture files. Duplicated are remapped to one common file by creating TextureFilenameMap<trackID>.ini list");
                Console.WriteLine("");
                Console.WriteLine("Copyright (c) 2021-2024, MIKA-N and RallySimFans team at www.rallysimfans.hu. All RBR map authors can use this tool for free. Use at your own risk. Not for commercial use without a permission from the author.");
                Console.WriteLine("");
                Console.WriteLine("Usage: Remap files, but does NOT delete duplicated files from textures.rbz");
                Console.WriteLine("   RemapIdenticalRBRTextures.exe -rbrFolder \"c:\\games\\Richard Burns Rally\" -track 972 -backupFolder c:\\backup\\rbr");
                Console.WriteLine("");
                Console.WriteLine("Usage: Remap files and DELETES duplicated files from textures.rbz");
                Console.WriteLine("   RemapIdenticalRBRTextures.exe -rbrFolder c:\\games\\rsf -track 972 -backupFolder c:\\backup\\rbr -DeleteDuplicates -ForceUpdate");
                Console.WriteLine("");
                Console.WriteLine("Usage: Restore original textures.rbz content with duplicated files. If ForceUpdate option is missing then simulates the result and does not update anything");
                Console.WriteLine("   RemapIdenticalRBRTextures.exe -rbrFolder c:\\games\\rsf -track 320 321 -backupFolder c:\\backup\\rbr -Restore -ZipFast -ForceUpdate");
                Console.WriteLine("");
                System.Environment.Exit(1);
            }

            // Parse cmdline options
            for (int idx = 0; idx < args.Length; idx++)
            {
                if (args[idx].Equals("-deleteduplicates", StringComparison.InvariantCultureIgnoreCase))
                {
                    deleteDuplicatedFiles = true;
                }
                else if (args[idx].Equals("-forceupdate", StringComparison.InvariantCultureIgnoreCase))
                {
                    forceUpdate = true;
                }
                else if (args[idx].Equals("-zipfast", StringComparison.InvariantCultureIgnoreCase))
                {
                    zipFast = true;
                }
                else if (args[idx].Equals("-rbrfolder", StringComparison.InvariantCultureIgnoreCase) && args.Length > idx + 1)
                {
                    rbrFolder = args[++idx];
                }
                else if (args[idx].Equals("-track", StringComparison.InvariantCultureIgnoreCase) && args.Length > idx + 1)
                {
                    while (args.Length > idx + 1)
                    {
                        //  Break the loop when no more trackID values (list of trackIDs as a space char separated string list)
                        if (string.IsNullOrEmpty(args[idx + 1]) || args[idx + 1][0] == '-')
                            break;

                        idx++;
                        trackIDList.Add(int.Parse(args[idx]));
                    }
                }
                else if (args[idx].Equals("-backupfolder", StringComparison.InvariantCultureIgnoreCase) && args.Length > idx + 1)
                {
                    backupFolder = args[++idx];
                }
                else if (args[idx].Equals("-restore", StringComparison.InvariantCultureIgnoreCase))
                {
                    restoreOriginalFiles = true;
                }
            }

            if (string.IsNullOrEmpty(backupFolder))
            {
                Console.Write("ERROR. Missing -backupFolder cmdline option");
                retValue = 1;
            }
            else if (restoreOriginalFiles == false && trackIDList.Count > 0)
            {
                retValue = DeduplicateFiles.RemoveIdenticalTextures(rbrFolder, backupFolder, trackIDList, deleteDuplicatedFiles, forceUpdate);
            }
            else if (restoreOriginalFiles == true && deleteDuplicatedFiles == false && trackIDList.Count > 0)
            {
                retValue = RestoreFiles.RestoreIdenticalTextures(rbrFolder, backupFolder, trackIDList, forceUpdate, zipFast);
            }
            else
            {
                Console.Write("ERROR. Invalid or missing cmdline options. Run RemapIdenticalRBRTextures.exe to see all options.");
                retValue = 1;
            }

            System.Environment.Exit(retValue);
        }

    }
}
