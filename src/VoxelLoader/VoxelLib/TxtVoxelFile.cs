using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

public class TxtVoxelFile
{
    public List<Color> colorLookup = new List<Color>();

    public TxtVoxelFile()
    {
    }

    public TxtVoxelFile(string[] htmlColors)
    {
        colorLookup.AddRange(htmlColors.Select(x => Color.FromHtml('#' + x)));
    }

    public void Read(string content, Vector3I offset, VoxelWorld world)
    {
        string[] lines = content.Replace("\r", "").Split("\n");
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith('#'))
                continue;
            string[] values = lines[i].Split(' ');
            if (values.Length > 3)
            {
                int x = int.Parse(values[0]);
                int y = int.Parse(values[1]);
                int z = int.Parse(values[2]);
                string hex = values[3];
                // if (ColorUtility.TryParseHtmlString('#' + hex, out Color color))
                Color color = Color.FromHtml('#' + hex);
                {
                    int id = colorLookup.IndexOf(color);
                    if (id == -1)
                        id = 0;
                    // swap y and z
                    if (world.TryGetVoxel(offset.X + x, offset.Y + z, offset.Z + y, out Voxel vox))
                    {
                        vox.Id = (byte)id;
                        world.SetVoxelWithData(new Vector3I(offset.X + x, offset.Y + z, offset.Z + y), vox);
                    }
                }
            }
        }
    }

    public string Write(Vector3I start, Vector3I end, VoxelWorld world)
    {
        StringBuilder sb = new StringBuilder();
        for (int x = start.X; x < end.X; x++)
        {
            for (int y = start.Y; y < end.Y; y++)
            {
                for (int z = start.Z; z < end.Z; z++)
                {
                    if (world.TryGetVoxel(x, z, y, out Voxel vox) && vox.IsActive)
                    {
                        Color color = colorLookup[vox.Id];
                        string hex = color.ToHtml();
                        // swap y and z
                        sb.AppendLine($"{x} {z} {y} {hex}");
                    }
                }
            }
        }
        return sb.ToString();
    }
}