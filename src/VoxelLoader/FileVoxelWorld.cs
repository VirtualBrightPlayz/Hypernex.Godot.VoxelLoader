using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Godot;
using Hypernex.GodotVersion.VoxelProvider;
using SharpNBT;

public partial class FileVoxelWorld : VoxelWorld
{
    [Export]
    public byte[] blocksFile = Array.Empty<byte>();
    [Export]
    public byte[] worldFile = Array.Empty<byte>();

    public Dictionary<byte, byte> layerLookup = new Dictionary<byte, byte>();

    public override void _EnterTree()
    {
        // _ = ReadSchematic();
        ReadSchematic();
    }

    public void ReadSchematic()
    {
        /*
        string blocksTxt = Encoding.UTF8.GetString(blocksFile);
        Color[] blockColors = blocksTxt.Split('\n').Select(x => Color.FromHtml('#' + x)).ToArray();
        materials.Clear();
        foreach (var color in blockColors)
        {
            GD.PrintErr(color);
            materials.Add(new StandardMaterial3D()
            {
                AlbedoColor = color,
                VertexColorUseAsAlbedo = false,
            });
        }
        */
        string texturesFileName = Path.GetTempFileName();
        File.WriteAllBytes(texturesFileName, blocksFile);
        using TextureReader reader = new TextureReader(texturesFileName);
        layerLookup.Clear();
        materials.Clear();
        for (byte i = 0; i < byte.MaxValue; i++)
        {
            Image img = reader.GetImageForBlockName(reader.BlockIdToBlockName(i));
            Image.AlphaMode mode = img.DetectAlpha();
            layerLookup.TryAdd(i, (byte)(mode == Image.AlphaMode.None ? 0 : 1));
            materials.Add(new StandardMaterial3D()
            {
                VertexColorUseAsAlbedo = false,
                AlbedoTexture = ImageTexture.CreateFromImage(img),
                TextureFilter = BaseMaterial3D.TextureFilterEnum.NearestWithMipmaps,
            });
        }

        string worldFileName = Path.GetTempFileName();
        File.WriteAllBytes(worldFileName, worldFile);
        CompoundTag rootTag = NbtFile.Read(worldFileName, FormatOptions.Java);
        short width = (ShortTag)rootTag["Width"];
        short height = (ShortTag)rootTag["Height"];
        short length = (ShortTag)rootTag["Length"];
        ByteArrayTag blocks = (ByteArrayTag)rootTag["Blocks"];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                for (int z = 0; z < length; z++)
                {
                    int index = (y * length + z) * width + x;
                    Vector3I vpos = new Vector3I(x, y, z);
                    Chunk chunk = SpawnOrGetChunk(FloorPosition(vpos));
                    Vector3I voxPos = vpos - UnroundPosition(chunk.chunkPosition);
                    if (chunk.TryGetVoxel(voxPos.X, voxPos.Y, voxPos.Z, out Voxel vox))
                    {
                        if (blocks[index] < materials.Count)
                        {
                            vox.Id = blocks[index];
                        }
                        else
                        {
                            vox.Id = 0;
                            GD.PrintErr($"{x} {y} {z} {blocks[index]}");
                        }
                        SetVoxelWithData(chunk, voxPos, vox);
                    }
                }
    }

    public override Voxel SetVoxelData(Voxel vox, Vector3I pos)
    {
        vox.Layer = layerLookup[vox.Id];
        return base.SetVoxelData(vox, pos);
    }

    public void ReadTxt()
    {
        string blocksTxt = Encoding.UTF8.GetString(blocksFile);
        string worldTxt = Encoding.UTF8.GetString(worldFile);
        TxtVoxelFile txt = new TxtVoxelFile(blocksTxt.Split('\n'));
        materials.Clear();
        foreach (var color in txt.colorLookup)
        {
            materials.Add(new StandardMaterial3D()
            {
                AlbedoColor = color,
            });
        }
        int size = 8;
        for (int x = -size; x <= size; x++)
        {
            for (int y = -size; y <= size; y++)
            {
                for (int z = -size; z <= size; z++)
                {
                    SpawnOrGetChunk(new Vector3I(x, y, z));
                }
            }
        }
        txt.Read(worldTxt, Vector3I.Zero, this);
    }
}