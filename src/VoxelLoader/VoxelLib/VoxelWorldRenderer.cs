using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;

public partial class VoxelWorldRenderer : StaticBody3D
{
    [Export]
    public VoxelWorld world;
    private List<VoxelMesh> meshes = new List<VoxelMesh>();
    private List<MeshInstance3D> instances = new List<MeshInstance3D>();
    private List<CollisionShape3D> colliders = new List<CollisionShape3D>();
    private List<Chunk> chunks = new List<Chunk>();
    private ConcurrentQueue<VoxelMesh> meshesToMesh = new ConcurrentQueue<VoxelMesh>();
    private ConcurrentQueue<VoxelMesh> meshesToUpdate = new ConcurrentQueue<VoxelMesh>();
    private int chunkCount;
    [Export]
    public int renderDistance = 8;
    private Camera3D cam;
    public Vector3I lastPosition = Vector3I.One * 100;
    private Thread thread;
    private bool isRunning = false;

    public override void _EnterTree()
    {
        RequestReady();
    }

    public override void _Ready()
    {
        isRunning = true;
        thread = new Thread(MeshingThread);
        thread.Start();
    }

    public override void _ExitTree()
    {
        isRunning = false;
        thread?.Join();
    }

    public void MeshingThread()
    {
        while (IsInstanceValid(this) && isRunning)
        {
            if (meshesToMesh.TryDequeue(out VoxelMesh mesh))
            {
                while (!mesh.UpdateMesh())
                {
                    Thread.Yield();
                }
                meshesToUpdate.Enqueue(mesh);
            }
            Thread.Yield();
        }
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(cam))
        {
            cam = GetViewport().GetCamera3D();
            return;
        }
        int size = renderDistance * 2 + 1;
        int max = size * size * size;
        Vector3I camChunkPos = VoxelWorld.FloorPosition((Vector3I)cam.GlobalPosition);
        if (camChunkPos != lastPosition)
        {
            Vector3I minPos = camChunkPos - new Vector3I(renderDistance, renderDistance, renderDistance);
            Vector3I maxPos = camChunkPos + new Vector3I(renderDistance, renderDistance, renderDistance);
            HashSet<int> usedPositions = new HashSet<int>();
            for (int i = 0; i < chunkCount; i++)
            {
                if (chunks[i].chunkPosition.X >= minPos.X && chunks[i].chunkPosition.Y >= minPos.Y && chunks[i].chunkPosition.Z >= minPos.Z &&
                    chunks[i].chunkPosition.X <= maxPos.X && chunks[i].chunkPosition.Y <= maxPos.Y && chunks[i].chunkPosition.Z <= maxPos.Z)
                {
                    usedPositions.Add(chunks[i].chunkPosition.X + chunks[i].chunkPosition.Y * size + chunks[i].chunkPosition.Z * size * size);
                    continue;
                }
                instances[i].QueueFree();
                colliders[i].QueueFree();
                meshes.RemoveAt(i);
                instances.RemoveAt(i);
                colliders.RemoveAt(i);
                chunks.RemoveAt(i);
                i--;
                chunkCount--;
            }
            for (int x = -renderDistance; x <= renderDistance; x++)
                for (int y = -renderDistance; y <= renderDistance; y++)
                    for (int z = -renderDistance; z <= renderDistance; z++)
                    {
                        Vector3I chunkPos = camChunkPos + new Vector3I(x, y, z);
                        if (usedPositions.Contains(chunkPos.X + chunkPos.Y * size + chunkPos.Z * size * size))
                            continue;
                        if (world.TryGetChunk(chunkPos, out Chunk chunk))
                        {
                            chunks.Add(chunk);
                            VoxelMesh mesh = new VoxelMesh(world, chunk);
                            meshes.Add(mesh);
                            MeshInstance3D instance = new MeshInstance3D()
                            {
                                // Mesh = mesh.GetMesh(),
                                Position = VoxelWorld.UnroundPosition(chunk.chunkPosition),
                            };
                            AddChild(instance);
                            instances.Add(instance);
                            CollisionShape3D collider = new CollisionShape3D()
                            {
                                // Shape = mesh.GetShape(),
                                Position = VoxelWorld.UnroundPosition(chunk.chunkPosition),
                            };
                            AddChild(collider);
                            colliders.Add(collider);
                            meshesToMesh.Enqueue(mesh);
                        }
                    }
            chunkCount = chunks.Count;
        }
        for (int i = 0; i < chunkCount; i++)
        {
            if (chunks[i].shouldUpdate > 0)
            {
                meshesToMesh.Enqueue(meshes[i]);
                chunks[i].shouldUpdate = Mathf.Max(chunks[i].shouldUpdate - 1, 0);
                // chunks[i].shouldUpdate = 0;
            }
        }
        if (meshesToUpdate.TryDequeue(out VoxelMesh vmesh))
        {
            int i = meshes.IndexOf(vmesh);
            if (i != -1)
            {
                instances[i].Mesh = vmesh.GetMesh();
                colliders[i].Shape = vmesh.GetShape();
            }
        }
        lastPosition = camChunkPos;
    }
}