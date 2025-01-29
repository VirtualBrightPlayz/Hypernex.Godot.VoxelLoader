using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;

public struct Voxel
{
    public byte Id;
    public Color Emit;
    public bool IsActive => Id != 0;
    public byte Layer;
    public object UserData;

    public void Init()
    {
        Id = 0;
        Layer = 0;
        Emit = Color.Color8(0, 0, 0, 0);
        UserData = null;
    }
}

public sealed class Chunk
{
    public const int SIZE = 16;
    public bool IsInitialized = false;
    public Voxel[] voxels;
    public Color[] lights;
    public Color[] visibleLights;
    public Vector3I chunkPosition;
    public int shouldUpdate = 0;

    public bool TryGetVoxel(int x, int y, int z, out Voxel voxel)
    {
        voxel = default;
        if (!IsInitialized)
            return false;
        if (x < 0 || x >= SIZE || y < 0 || y >= SIZE || z < 0 || z >= SIZE)
            return false;
        voxel = voxels[x + y * SIZE + z * SIZE * SIZE];
        return true;
    }

    public void SetVoxel(int x, int y, int z, Voxel voxel)
    {
        if (!IsInitialized)
            return;
        if (x < 0 || x >= SIZE || y < 0 || y >= SIZE || z < 0 || z >= SIZE)
            return;
        voxels[x + y * SIZE + z * SIZE * SIZE] = voxel;
    }

    public bool TryGetLight(int x, int y, int z, out Color light)
    {
        light = default;
        if (!IsInitialized)
            return false;
        if (x < 0 || x >= SIZE || y < 0 || y >= SIZE || z < 0 || z >= SIZE)
            return false;
        light = lights[x + y * SIZE + z * SIZE * SIZE];
        return true;
    }

    public bool TryGetVisibleLight(int x, int y, int z, out Color light)
    {
        light = default;
        if (!IsInitialized)
            return false;
        if (x < 0 || x >= SIZE || y < 0 || y >= SIZE || z < 0 || z >= SIZE)
            return false;
        light = visibleLights[x + y * SIZE + z * SIZE * SIZE];
        return true;
    }

    public void SetLight(int x, int y, int z, Color light)
    {
        if (!IsInitialized)
            return;
        if (x < 0 || x >= SIZE || y < 0 || y >= SIZE || z < 0 || z >= SIZE)
            return;
        lights[x + y * SIZE + z * SIZE * SIZE] = light;
    }

    public void UpdateLightBuffers()
    {
        if (!IsInitialized)
            return;
        Array.Copy(lights, visibleLights, visibleLights.Length);
        Array.Fill(lights, new Color(0f, 0f, 0f, 0f));
    }

    public void QueueUpdateMesh()
    {
        shouldUpdate++;
    }

    private void Init()
    {
        if (IsInitialized)
            return;
        voxels = new Voxel[SIZE * SIZE * SIZE];
        lights = new Color[SIZE * SIZE * SIZE];
        visibleLights = new Color[SIZE * SIZE * SIZE];
        for (int i = 0; i < voxels.Length; i++)
        {
            voxels[i].Init();
            visibleLights[i] = lights[i] = voxels[i].Emit;
        }
        IsInitialized = true;
    }

    public Chunk()
    {
    }

    public Chunk(Vector3I pos)
    {
        // new Thread(Init).Start();
        Init();
        chunkPosition = pos;
    }
}