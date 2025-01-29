using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class DemoVoxelWorld : VoxelWorld
{
    [Export]
    public Godot.Collections.Array<VoxelAsset> voxelTypes = new Godot.Collections.Array<VoxelAsset>();
    [Export]
    public int minMaterial = 1;
    [Export]
    public int maxMaterial = 2;
    [Export]
    public int renderDistance = 5;
    private Vector3 lastPosition;
    public FastNoiseLite heightNoise;
    public FastNoiseLite heightNoise2;
    public FastNoiseLite biomeNoise;

    private bool mapGenRunning = false;

    private async Task TryGenChunk(Vector3I pos)
    {
        while (mapGenRunning)
            await Task.Delay(100);
        mapGenRunning = true;
        List<Chunk> meshes = new List<Chunk>();
        for (int x = -renderDistance; x <= renderDistance; x++)
        {
            for (int y = 0; y <= renderDistance; y++)
            {
                for (int z = -renderDistance; z <= renderDistance; z++)
                {
                    Chunk mesh = SpawnChunk(new Vector3I(pos.X + x, y, pos.Z + z));
                    if (mesh != null)
                        meshes.Add(mesh);
                }
            }
        }
        Task[] meshTasks = new Task[meshes.Count];
        for (int i = 0; i < meshes.Count; i++)
        {
            meshTasks[i] = new Task(j =>
            {
                GenerateVoxels(meshes[(int)j]);
            }, i);
            meshTasks[i].Start();
        }
        await Task.WhenAll(meshTasks);
        foreach (var chunk in meshes)
        {
            chunk.QueueUpdateMesh();
        }
        mapGenRunning = false;
    }

    private async Task GenerateMap(bool actually)
    {
        GD.Print("Generating Map...");
        // hasMap = true;
        heightNoise = new FastNoiseLite(seed);
        heightNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
        heightNoise.SetFrequency(0.005f);

        heightNoise2 = new FastNoiseLite(seed);
        heightNoise2.SetFrequency(0.005f);
        heightNoise2.SetNoiseType(FastNoiseLite.NoiseType.Cellular);

        biomeNoise = new FastNoiseLite(seed + 1);
        biomeNoise.SetFrequency(0.002f);
        biomeNoise.SetFractalType(FastNoiseLite.FractalType.FBm);

        if (actually)
        {
            await TryGenChunk(Vector3I.Zero);
        }
    }

    public override void GenerateVoxels(Chunk chunk)
    {
        for (int x = 0; x < Chunk.SIZE; x++)
        {
            for (int z = 0; z < Chunk.SIZE; z++)
            {
                Vector3I slicePos = chunk.chunkPosition * Chunk.SIZE + new Vector3I(x, 0, z);
                float height = GetHeight(slicePos.X, slicePos.Z) * Chunk.SIZE * renderDistance;
                // int biome = GetBiome(slicePos.X, slicePos.Z);
                for (int y = 0; y < Chunk.SIZE; y++)
                {
                    Vector3I worldPos = chunk.chunkPosition * Chunk.SIZE + new Vector3I(x, y, z);
                    if (TryGetVoxel(x, y, z, out Voxel vox))
                    {
                        if (worldPos.Y < 3 && worldPos.Y < height)
                        {
                            vox.Id = 1;//floorBlockId;
                            SetVoxelWithData(worldPos, vox);
                        }
                        else if (worldPos.Y < height)
                        {
                            vox.Id = 2;
                            // vox.Id = (byte)types.IndexOf(biomes[biome].surface);
                            SetVoxelWithData(worldPos, vox);
                        }
                        else
                        {
                            vox.Id = 0;
                            SetVoxelWithData(worldPos, vox);
                        }
                    }
                }
            }
        }
    }

    public float GetHeight(int x, int z)
    {
        return (heightNoise.GetNoise(x, z) * 0.5f + 0.5f) * (heightNoise2.GetNoise(x, z) * 0.5f + 0.5f);
    }

    public int GetBiome(int x, int z)
    {
        return Mathf.FloorToInt((biomeNoise.GetNoise(x, z) * 0.5f + 0.5f) * (maxMaterial - minMaterial + 1) + minMaterial);
    }

    public override Voxel SetVoxelData(Voxel vox, Vector3I pos)
    {
        if (voxelTypes[vox.Id].lit)
            vox.Emit = voxelTypes[vox.Id].emission;
        else
            vox.Emit = new Color(0f, 0f, 0f, vox.Emit.A);
        vox.Layer = voxelTypes[vox.Id].layer;
        vox.UserData = null;
        // TODO: sand
        return vox;
    }

    public override void _Ready()
    {
        materials.Clear();
        for (int i = 0; i < voxelTypes.Count; i++)
            materials.Add(voxelTypes[i].material);
        _ = GenerateMap(true);
    }

    public override void _PhysicsProcess(double delta)
    {
        Transform3D cam = GetViewport().GetCamera3D().GlobalTransform;
        Vector3I pos = RoundPosition(cam.Origin);
        Vector3I lastPos = RoundPosition(lastPosition);
        if (pos != lastPos)
        {
            _ = TryGenChunk(pos);
        }
        lastPosition = cam.Origin;
    }

    public override void _Process(double delta)
    {
        UpdateTasks();
        // UpdateMapGen();
        // UpdateTimeCycle();
        Tick();
    }
}