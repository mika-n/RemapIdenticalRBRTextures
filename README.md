# RemapIdenticalRBRTextures

This tool identifies identical DDS texture files in RICHARD BURNS RALLY textures.rbz archive files, removes duplicates and re-packs the RBZ file.

*What are the benefits of doing all this for RBR maps?*
- Smaller map pack size in bytes (the size of track-1234_M/N/E/O_textures.rbz files is reduced when there are no duplicated files). 
- Smaller map pack saves download bandwidth and diskspace in PC.
- Faster map loading because RBR doesn't re-load a texture if an identical copy is already loaded, plus RBR can read smaller textures.rbz files a bit faster (couple nano seconds faster in SSD systems, 5 nano seconds in historical HDD systems).
- Wallaby and BlenderRBRExporter doesn't identify identical files yet, so this tool fills the gap and works with all maps created with any tool (even for old maps released years ago).

*How this works?*
- Extracts map specific textures.rbz files to a temporary folder
- Identifies duplicated copies of DDS file, removes duplicated copies and leaves just one copy of the DDS texture file.
- Creates TextureFilenameMap<MapID>.ini file with links between a removed DDS file and the one preserved copy of the identical file.
- RBR (tuned with NGPCarMenu plugin) uses the generated TextureFilenameMap<MapID>.ini file at stage load time to instruct RBR to use a remapped texture DDS file instead of the deleted duplicated copy. The remap link goes even between textures_O.rbz and texture_M.rbz files to eliminate duplicated DDS files between skybox rbz file.

Note! Identical file here means that the file content is one-on-one identical in a binary level (not a filename match).
 
 ```
 Shows usage info:
   RemapIdenticalRBRTextures.exe   

 Identifies duplicated textures in track 972 map pack (Maps\Tracks.ini [Map972] map), but does NOT update the map pack. 
 This command can be used to "simulate" the process and see the end result in c:\backup\rbr folder without actually modifying anything.
   RemapIdenticalRBRTextures.exe -rbrFolder "c:\games\Richard Burns Rally" -track 972 -backupFolder c:\backup\rbr

 Identifies duplicated textures in track 972 map pack, deletes duplicates and modifies the original RBZ map pack (-DeleteDuplicates option)
   RemapIdenticalRBRTextures.exe -rbrFolder "c:\games\Richard Burns Rally" -track 972 -backupFolder c:\backup\rbr -DeleteDuplicates
```

 
*Recommended map delivery folder structure with RallySimFans RBR mode*
 - RallySimFans (RSF) nowadays delivers new classic maps using the following folder logic (note the stage specific subfolder in RBR Maps folder and stage specific TracksXXX.ini, TrackSettingsXXX.ini and TextureFilenameMapXXX.ini files here. No need to merge values at installation time to the common maps\tracks.ini and maps\tracksettings.ini files)
 
 ```
 c:\games\rbr\Maps\
 c:\games\rbr\Maps\427-Zaton\
 c:\games\rbr\Maps\427-Zaton\Tracks427.ini
 c:\games\rbr\Maps\427-Zaton\TrackSettings427.ini
 c:\games\rbr\Maps\427-Zaton\TextureFilenameMap427.ini
 c:\games\rbr\Maps\427-Zaton\track-427_N_textures.rbz
 c:\games\rbr\Maps\427-Zaton\track-427_N.lbs
 ```
  
Copyright (c) 2021-2022 MIKA-N. All rights reserved. This is a free tool for all RBR map authors, but not for commercial use without a permission from the author. Use at your own risk.
 
RallySimFans - www.rallysimfans.hu
