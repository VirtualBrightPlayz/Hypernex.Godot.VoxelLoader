using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class VoxelWorld : Node
{
    [Export]
    public Godot.Collections.Array<Material> materials = new Godot.Collections.Array<Material>();
    protected HashSet<Vector3I> chunkPositions = new HashSet<Vector3I>();
    protected Dictionary<Vector3I, Chunk> chunks = new Dictionary<Vector3I, Chunk>();
    [Export]
    public int seed = 1337;
    protected bool genRunning = false;
    protected Queue<Vector3I> chunkUpdateQueue = new Queue<Vector3I>();
    protected Queue<Vector3I> voxelsToTick = new Queue<Vector3I>();
    protected Queue<(Vector3I, byte)> voxelUpdateQueue = new Queue<(Vector3I, byte)>();

    [Export]
    public int tickRate = 20;
    [Export]
    public double tickSpeed = 1d;
    [Export]
    public int maxTicks = 60;
    protected double lastTickTime;
    protected bool tickRunning = false;

    public static Vector3I RoundPosition(Vector3 pos)
    {
        Vector3 pos2 = pos - Vector3.One * Chunk.SIZE / 2f;
        return new Vector3I(Mathf.RoundToInt(pos2.X / Chunk.SIZE), Mathf.RoundToInt(pos2.Y / Chunk.SIZE), Mathf.RoundToInt(pos2.Z / Chunk.SIZE));
    }

    public static Vector3I FloorPosition(Vector3I pos)
    {
        Vector3I pos2 = pos;
        return new Vector3I(Mathf.FloorToInt((float)pos2.X / Chunk.SIZE), Mathf.FloorToInt((float)pos2.Y / Chunk.SIZE), Mathf.FloorToInt((float)pos2.Z / Chunk.SIZE));
    }

    public static Vector3I UnroundPosition(Vector3I pos)
    {
        return pos * Chunk.SIZE;
    }

    public static Vector3I GetVoxelPosition(Vector3 pos)
    {
        return new Vector3I(Mathf.FloorToInt(pos.X), Mathf.FloorToInt(pos.Y), Mathf.FloorToInt(pos.Z));
    }

    public bool TryGetChunk(Vector3I pos, out Chunk chunk)
    {
        chunk = default;
        if (chunks.TryGetValue(pos, out chunk))
            return true;
        return false;
    }

    public Voxel GetVoxelOrDefault(int x, int y, int z)
    {
        Voxel vox;
        if (!TryGetVoxel(x, y, z, out vox))
            vox.Init();
        return vox;
    }

    public Voxel GetVoxelOrDefault(Chunk chunk, int x, int y, int z)
    {
        Voxel vox;
        if (!TryGetVoxel(chunk, x, y, z, out vox))
            vox.Init();
        return vox;
    }

    public Color GetVisibleLightOrZero(Chunk chunk, int x, int y, int z)
    {
        Vector3I pos = new Vector3I(x, y, z) + UnroundPosition(chunk.chunkPosition);
        Vector3I chunkPos = FloorPosition(pos);
        if (chunks.TryGetValue(chunkPos, out Chunk mesh))
        {
            Vector3I voxelPos = GetVoxelPosition(pos) - UnroundPosition(chunkPos);
            if (mesh.TryGetVisibleLight(voxelPos.X, voxelPos.Y, voxelPos.Z, out Color light))
                return light;
        }
        // if (TryGetVoxelLight(pos, out Voxel _, out Color32 light))
            // return light;
        return new Color(0, 0, 0, 0);
    }

    public bool TryGetVoxel(int x, int y, int z, out Voxel voxel)
    {
        return TryGetVoxel(new Vector3I(x, y, z), out voxel);
    }

    public bool TryGetVoxel(Chunk chunk, int x, int y, int z, out Voxel voxel)
    {
        voxel = default;
        if (chunk != null)
        {
            Vector3I voxelPos = new Vector3I(x, y, z) + UnroundPosition(chunk.chunkPosition);
            return TryGetVoxel(voxelPos.X, voxelPos.Y, voxelPos.Z, out voxel);
        }
        return false;
    }

    public bool TryGetVoxel(Vector3I pos, out Voxel voxel)
    {
        voxel = default;
        Vector3I chunkPos = FloorPosition(pos);
        if (chunks.TryGetValue(chunkPos, out Chunk chunk))
        {
            Vector3I voxelPos = GetVoxelPosition(pos) - UnroundPosition(chunkPos);
            return chunk.TryGetVoxel(voxelPos.X, voxelPos.Y, voxelPos.Z, out voxel);
        }
        return false;
    }

    public bool TryGetVoxelLight(Vector3I pos, out Voxel voxel, out Color light)
    {
        voxel = default;
        light = default;
        Vector3I chunkPos = FloorPosition(pos);
        if (chunks.TryGetValue(chunkPos, out Chunk chunk))
        {
            Vector3I voxelPos = GetVoxelPosition(pos) - UnroundPosition(chunkPos);
            return chunk.TryGetVoxel(voxelPos.X, voxelPos.Y, voxelPos.Z, out voxel) && chunk.TryGetLight(voxelPos.X, voxelPos.Y, voxelPos.Z, out light);
        }
        return false;
    }

    public bool IsFaceVisible(Chunk chunk, int x, int y, int z, byte layer)
    {
        if (!chunk.TryGetVoxel(x, y, z, out Voxel vox))
        {
            Vector3I position = UnroundPosition(chunk.chunkPosition);
            return IsFaceVisible(position.X + x, position.Y + y, position.Z + z, layer);
        }
        return !vox.IsActive || layer != vox.Layer;
    }

    public bool IsFaceVisible(int x, int y, int z, byte layer)
    {
        if (!TryGetVoxel(x, y, z, out Voxel vox))
            return true;
        return !vox.IsActive || layer != vox.Layer;
    }

    public virtual void UpdateTasks()
    {
        if (!genRunning && chunkUpdateQueue.TryDequeue(out Vector3I chunkPos))
        {
            _ = UpdateChunks(chunkPos, true, true);
        }
    }

    public virtual void Tick()
    {
        if (tickRunning)
            return;
        tickRunning = true;
        // double rt = Time.timeAsDouble;
        double rt = Time.GetTicksMsec() / 1000d;
        int ticks = 0;
        while (rt > lastTickTime)
        {
            double delta = tickSpeed / tickRate;
            if (ticks < maxTicks)
                TickWorld(delta);
            lastTickTime += 1d / tickRate;
            ticks++;
        }
        if (ticks >= maxTicks)
        {
            GD.PushWarning($"Max ticks reached! ({ticks} ticks)");
        }
        tickRunning = false;
    }

    public virtual void TickWorld(double delta)
    {
        Vector3I[] queue = voxelsToTick.ToArray();
        voxelsToTick.Clear();
        for (int i = 0; i < queue.Length; i++)
        {
            Vector3I voxelPos = queue[i];
            if (TryGetVoxel(voxelPos, out Voxel voxel))
            {
                TickVoxel(delta, voxelPos, voxel);
            }
        }
        while (voxelUpdateQueue.TryDequeue(out (Vector3I pos, byte id) voxelData))
        {
            if (TryGetVoxel(voxelData.pos, out Voxel voxel))
            {
                voxel.Id = voxelData.id;
                SetVoxelWithData(voxelData.pos, voxel);
                QueueTickVoxelArea(voxelData.pos);
                QueueUpdateChunks(FloorPosition(voxelData.pos), false);
            }
        }
    }

    public virtual void TickVoxel(double delta, Vector3I voxelPos, Voxel voxel)
    {
    }

    public void QueueUpdateChunks(Vector3I chunkPos, bool now)
    {
        if (now)
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                        if (chunks.TryGetValue(chunkPos + new Vector3I(x, y, z), out Chunk chunk))
                            chunk.QueueUpdateMesh();
        if (chunkUpdateQueue.Contains(chunkPos))
            return;
        chunkUpdateQueue.Enqueue(chunkPos);
    }

    public void QueueSetVoxel(Vector3I pos, byte id)
    {
        voxelUpdateQueue.Enqueue((pos, id));
    }

    public void QueueTickVoxel(Vector3I pos)
    {
        voxelsToTick.Enqueue(pos);
    }

    public void QueueTickVoxelArea(Vector3I pos)
    {
        voxelsToTick.Enqueue(pos);
        voxelsToTick.Enqueue(pos + Vector3I.Up);
        voxelsToTick.Enqueue(pos + Vector3I.Down);
        voxelsToTick.Enqueue(pos + Vector3I.Left);
        voxelsToTick.Enqueue(pos + Vector3I.Right);
        voxelsToTick.Enqueue(pos + Vector3I.Forward);
        voxelsToTick.Enqueue(pos + Vector3I.Back);
    }

    public void SetVoxelRaw(Vector3I pos, Voxel voxel)
    {
        Vector3I chunkPos = FloorPosition(pos);
        if (chunks.TryGetValue(chunkPos, out Chunk mesh))
        {
            Vector3I voxelPos = GetVoxelPosition(pos) - UnroundPosition(chunkPos);
            mesh.SetVoxel(voxelPos.X, voxelPos.Y, voxelPos.Z, voxel);
        }
    }

    public void SetVoxelLightRaw(Vector3I pos, Voxel voxel, Color light)
    {
        Vector3I chunkPos = FloorPosition(pos);
        if (chunks.TryGetValue(chunkPos, out Chunk mesh))
        {
            Vector3I voxelPos = GetVoxelPosition(pos) - UnroundPosition(chunkPos);
            mesh.SetVoxel(voxelPos.X, voxelPos.Y, voxelPos.Z, voxel);
            mesh.SetLight(voxelPos.X, voxelPos.Y, voxelPos.Z, light);
        }
    }

    public void SetVoxelWithData(Vector3I pos, Voxel voxel)
    {
        Vector3I chunkPos = FloorPosition(pos);
        if (chunks.TryGetValue(chunkPos, out Chunk mesh))
        {
            Vector3I voxelPos = GetVoxelPosition(pos) - UnroundPosition(chunkPos);
            mesh.SetVoxel(voxelPos.X, voxelPos.Y, voxelPos.Z, SetVoxelData(voxel, pos));
        }
    }

    // pos in world space
    public void SetVoxelWithData(Chunk mesh, Vector3I pos, Voxel voxel)
    {
        Vector3I voxelPos = pos;
        mesh.SetVoxel(voxelPos.X, voxelPos.Y, voxelPos.Z, SetVoxelData(voxel, UnroundPosition(mesh.chunkPosition) + pos));
    }

    public virtual Voxel SetVoxelData(Voxel vox, Vector3I pos)
    {
        return vox;
    }

    public virtual void ProcessLight(Vector3I pos)
    {
    }

    public async Task UpdateChunks(Vector3I chunkPos, bool updateMeshes, bool updateLightmap, int area = 1)
    {
        if (genRunning)
            return;
        genRunning = true;
        Queue<Vector3I> subLights = new Queue<Vector3I>();
        Queue<(Vector3I, Color)> lights = new Queue<(Vector3I, Color)>();
        if (updateLightmap)
        {
            for (int x = -area; x <= area; x++)
                for (int y = -area; y <= area; y++)
                    for (int z = -area; z <= area; z++)
                        if (chunks.TryGetValue(chunkPos + new Vector3I(x, y, z), out Chunk chunk))
                        {
                            for (int x2 = 0; x2 < Chunk.SIZE; x2++)
                                for (int y2 = 0; y2 < Chunk.SIZE; y2++)
                                    for (int z2 = 0; z2 < Chunk.SIZE; z2++)
                                    {
                                        if (chunk.TryGetVoxel(x2, y2, z2, out Voxel vox))
                                        {
                                            if (vox.Emit.A8 == 0)
                                            {
                                                chunk.SetVoxel(x2, y2, z2, vox);
                                                continue;
                                            }
                                            if (vox.Emit.A8 == 0 || (vox.Emit.R8 == 0 && vox.Emit.G8 == 0 && vox.Emit.B8 == 0))
                                            {
                                                subLights.Enqueue(UnroundPosition(chunkPos + new Vector3I(x, y, z)) + new Vector3I(x2, y2, z2));
                                                vox.Emit.A8 = 0;
                                            }
                                            else
                                                lights.Enqueue((UnroundPosition(chunkPos + new Vector3I(x, y, z)) + new Vector3I(x2, y2, z2), vox.Emit));
                                            chunk.SetVoxel(x2, y2, z2, vox);
                                        }
                                    }
                        }
        }
        while (subLights.Count != 0)
        {
            Vector3I pos2 = subLights.Dequeue();
            ProcessLight(pos2);
        }
        while (lights.Count != 0)
        {
            (Vector3I pos2, Color col) = lights.Dequeue();
            ProcessLight(pos2);
            await Task.Run(() => UpdateVoxelLightmap(pos2, col));
        }
        {
            for (int x = -area; x <= area; x++)
                for (int y = -area; y <= area; y++)
                    for (int z = -area; z <= area; z++)
                        if (chunks.TryGetValue(chunkPos + new Vector3I(x, y, z), out Chunk chunk))
                        {
                            if (updateLightmap)
                                chunk.UpdateLightBuffers();
                            if (updateMeshes)
                                chunk.QueueUpdateMesh();
                        }
        }
        genRunning = false;
    }

    public Chunk SpawnOrGetChunk(Vector3I pos)
    {
        if (chunkPositions.Contains(pos) && chunks.TryGetValue(pos, out Chunk ch))
        {
            return ch;
        }
        else
        {
            // VoxelMesh chunk = Instantiate(prefab, UnroundPosition(pos), Quaternion.identity, transform);
            // chunk.world = this;
            // chunk.gameObject.SetActive(true);
            // chunk.Setup();
            Chunk chunk = new Chunk(pos);
            chunks.Add(pos, chunk);
            chunkPositions.Add(pos);
            return chunk;
        }
    }

    public Chunk SpawnChunk(Vector3I pos)
    {
        if (!chunkPositions.Contains(pos))
        {
            Chunk chunk = new Chunk(pos);
            chunks.Add(pos, chunk);
            chunkPositions.Add(pos);
            return chunk;
        }
        return null;
    }

    public virtual void GenerateVoxels(Chunk chunk)
    {
    }

    public override void _Process(double delta)
    {
        UpdateTasks();
        Tick();
    }

    #region Lighting

    public void UpdateVoxelLightmap(Vector3I voxPos, Color baseLight)
    {
        Queue<(Vector3I, Color)> queue = new Queue<(Vector3I, Color)>();
        queue.Enqueue((voxPos, baseLight));
        List<Vector3I> list = new List<Vector3I>();
        while (queue.Count != 0)
        {
            (Vector3I pos, Color light) = queue.Dequeue();
            if (list.Contains(pos) || light.A8 <= 0)
                continue;
            list.Add(pos);
            if (!TryGetVoxelLight(pos, out Voxel vox, out Color voxLight))
                continue;
            if (vox.IsActive && pos != voxPos)
                continue;
            if (voxLight.A8 > light.A8)
                continue;
            float amount = (float)light.A8 / baseLight.A8;
            light.R8 = (byte)(baseLight.R8 * amount);
            light.G8 = (byte)(baseLight.G8 * amount);
            light.B8 = (byte)(baseLight.B8 * amount);
            voxLight = light;
            light.A8--;
            SetVoxelLightRaw(pos, vox, voxLight);
            if (!list.Contains(pos + Vector3I.Up))
                queue.Enqueue((pos + Vector3I.Up, light));
            if (!list.Contains(pos + Vector3I.Down))
                queue.Enqueue((pos + Vector3I.Down, light));
            if (!list.Contains(pos + Vector3I.Left))
                queue.Enqueue((pos + Vector3I.Left, light));
            if (!list.Contains(pos + Vector3I.Right))
                queue.Enqueue((pos + Vector3I.Right, light));
            if (!list.Contains(pos + Vector3I.Forward))
                queue.Enqueue((pos + Vector3I.Forward, light));
            if (!list.Contains(pos + Vector3I.Back))
                queue.Enqueue((pos + Vector3I.Back, light));
        }
    }

    #endregion
}