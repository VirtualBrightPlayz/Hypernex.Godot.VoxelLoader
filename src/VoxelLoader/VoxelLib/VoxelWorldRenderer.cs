using System;
using System.Collections.Generic;
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
    private int chunkCount;
    [Export]
    public int renderDistance = 8;
    private Camera3D cam;
    public Vector3I lastPosition = Vector3I.One * 100;

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
                                Mesh = mesh.GetMesh(),
                                Position = VoxelWorld.UnroundPosition(chunk.chunkPosition),
                            };
                            AddChild(instance);
                            instances.Add(instance);
                            CollisionShape3D collider = new CollisionShape3D()
                            {
                                Shape = mesh.GetShape(),
                                Position = VoxelWorld.UnroundPosition(chunk.chunkPosition),
                            };
                            AddChild(collider);
                            colliders.Add(collider);
                            _ = mesh.UpdateMeshAsync();
                        }
                    }
            chunkCount = chunks.Count;
        }
        for (int i = 0; i < chunkCount; i++)
        {
            if (chunks[i].shouldUpdate > 0)
                _ = meshes[i].UpdateMeshAsync();
            instances[i].Mesh = meshes[i].GetMesh();
            colliders[i].Shape = meshes[i].GetShape();
        }
        lastPosition = camChunkPos;
    }
}