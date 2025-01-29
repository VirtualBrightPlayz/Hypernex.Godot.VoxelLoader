dotnet build -c Debug

Copy-Item -Path .godot/mono/temp/bin/Debug/Hypernex.Godot.VoxelLoader.dll -Destination $env:APPDATA/Godot/app_userdata/Hypernex.Godot/Plugins -Force
Copy-Item -Path .godot/mono/temp/bin/Debug/Hypernex.Godot.VoxelLoader.pdb -Destination $env:APPDATA/Godot/app_userdata/Hypernex.Godot/Plugins -Force
Copy-Item -Path .godot/mono/temp/bin/Debug/SharpNBT.dll -Destination $env:APPDATA/Godot/app_userdata/Hypernex.Godot/Plugins -Force
