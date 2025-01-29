using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Hypernex.GodotVersion.VoxelProvider;

public partial class TextureReader : IDisposable
{
    public ZipReader zip;
    public Dictionary<byte, string> lookup = new Dictionary<byte, string>();

    public TextureReader(string path)
    {
        string[] lines = Plugin.GetItemsTsv().Replace("\r", "").Split('\n');
        foreach (var line in lines)
        {
            string[] vals = line.Split('\t');
            if (vals.Length <= 3)
                continue;
            if (vals[1] != "0")
                continue;
            if (byte.TryParse(vals[0], out byte b) && !lookup.ContainsKey(b))
                lookup.Add(b, vals[3]);
        }
        zip = new ZipReader();
        zip.Open(path);
    }

    public void Dispose()
    {
        zip.Close();
    }

    public string BlockIdToBlockName(byte id)
    {
        if (lookup.TryGetValue(id, out string val))
            return val;
        return string.Empty;
    }

    public Image GetImageForBlockName(string name)
    {
        string file = $"assets/minecraft/textures/block/{name}.png";
        Image img = Image.CreateEmpty(64, 64, false, Image.Format.Rgb8);
        if (zip.FileExists(file, false))
        {
            byte[] data = zip.ReadFile(file, false);
            img.LoadPngFromBuffer(data);
        }
        return img;
    }
}