using Godot;

[GlobalClass]
public partial class VoxelAsset : Resource
{
    [Export]
    public Material material;
    [Export]
    public bool lit = false;
    [Export]
    public Color emission = Color.Color8(255, 255, 255, 16);
    [Export]
    public byte layer = 0;
    [Export]
    public bool sand = false;
}