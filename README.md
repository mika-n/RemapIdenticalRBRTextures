# RemapIdenticalRBRTextures

This tool identifies identical DDS texture files in RICHARD BURNS RALLY textures.rbz archive files, removes duplicates and re-packs the RBZ file.

What are the benefits of doing all this for RBR maps?
- Smaller map pack size in bytes (the size of track-1234_M/N/E/O_textures.rbz files is reduced when there are no duplicated files). 
- Smaller map pack saves download bandwidth and diskspace in PC.
- Faster map loading because RBR doesn't re-load a texture if an identical copy is already loaded, plus RBR can read smaller textures.rbz files a bit faster (couple nano seconds faster in SSD systems, 5 nano seconds in historical HDD systems).
- Wallaby and BlenderRBRExporter doesn't identify identical files yet, so this tool fills the gap and works with all maps created with any tool (even for old maps released years ago).

How this works?
- Extracts map specific textures.rbz files to a temporary folder
- Identifies duplicated copies of DDS file, removes duplicated copies and leaves just one copy of the DDS texture file.
- Creates TextureFilenameMap<MapID>.ini file with links between a removed DDS file and the one preserved copy of the identical file.
- RallySimFans (RSF) plugin uses the TextureFilenameMap<MapID>.ini file to instruct RBR to use a remapped texture instead of the delete duplicated copy.

Note! Identical file here means that the file content is one-on-one identical in a binary level (not a filename match)
 
 Shows usage info:
   RemapIdenticalRBRTextures.exe   

 Identifies duplicated textures in track 972 map pack (Maps\Tracks.ini [Map972] map), but does NOT update the map pack. 
 This command can be used to "simulate" the process and see the end result in c:\backup\rbr folder without actually modifying anything.
   RemapIdenticalRBRTextures.exe -rbrFolder "c:\games\Richard Burns Rally" -track 972 -backupFolder c:\backup\rbr

 Identifies duplicated textures in track 972 map pack, deletes duplicates and modifies the original RBZ map pack (-DeleteDuplicates option)
   RemapIdenticalRBRTextures.exe -rbrFolder "c:\games\Richard Burns Rally" -track 972 -backupFolder c:\backup\rbr -DeleteDuplicates
   
Copyright (c) 2021-2022 MIKA-N. All rights reserved. This is a free tool for all RBR map authors, but not for commercial use without a permission from the author. Use at your own risk.
 
RallySimFans - www.rallysimfans.hu
