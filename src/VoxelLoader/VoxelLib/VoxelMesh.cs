using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Mutex = System.Threading.Mutex;

public class VoxelMesh
{
    private List<Vector3> verts = new List<Vector3>();
    private List<Vector3> normals = new List<Vector3>();
    private Dictionary<int, List<int>> trisLookup = new Dictionary<int, List<int>>();
    private List<Vector2> uvs = new List<Vector2>();
    private List<Color> colors = new List<Color>();
    public Chunk chunk;
    public VoxelWorld world;
    private ArrayMesh mesh;
    private ConcavePolygonShape3D shape;
    private Material[] materials = new Material[0];

    [StructLayout(LayoutKind.Sequential)]
    public struct ChunkVertex
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector4 tangent;
        public Color color;
        public Vector2 uv;
    }

    private void AddChunk()
    {
        bool[] visibleFaces = new bool[6];
        Color[] lightFaces = new Color[6];
        for (int x = 0; x < Chunk.SIZE; x++)
        {
            for (int y = 0; y < Chunk.SIZE; y++)
            {
                for (int z = 0; z < Chunk.SIZE; z++)
                {
                    AddVoxel(x, y, z, ref visibleFaces, ref lightFaces);
                }
            }
        }
    }

    private void AddVoxel(int x, int y, int z, ref bool[] visibleFaces, ref Color[] lightFaces)
    {
        if (chunk.TryGetVoxel(x, y, z, out Voxel vox) && vox.IsActive)
        {
            visibleFaces[0] = world.IsFaceVisible(chunk, x, y + 1, z, vox.Layer); // up
            visibleFaces[1] = world.IsFaceVisible(chunk, x, y - 1, z, vox.Layer); // down
            visibleFaces[2] = world.IsFaceVisible(chunk, x - 1, y, z, vox.Layer); // left
            visibleFaces[3] = world.IsFaceVisible(chunk, x + 1, y, z, vox.Layer); // right
            visibleFaces[4] = world.IsFaceVisible(chunk, x, y, z + 1, vox.Layer); // forward
            visibleFaces[5] = world.IsFaceVisible(chunk, x, y, z - 1, vox.Layer); // back

            lightFaces[0] = world.GetVisibleLightOrZero(chunk, x, y + 1, z);
            lightFaces[1] = world.GetVisibleLightOrZero(chunk, x, y - 1, z);
            lightFaces[2] = world.GetVisibleLightOrZero(chunk, x - 1, y, z);
            lightFaces[3] = world.GetVisibleLightOrZero(chunk, x + 1, y, z);
            lightFaces[4] = world.GetVisibleLightOrZero(chunk, x, y, z + 1);
            lightFaces[5] = world.GetVisibleLightOrZero(chunk, x, y, z - 1);

            for (int i = 0; i < 6; i++)
            {
                if (visibleFaces[i])
                {
                    AddFaceData(x, y, z, i, vox, lightFaces[i]);
                }
            }
        }
    }

    private void AddFaceData(int x, int y, int z, int faceIndex, Voxel vox, Color light)
    {
        Vector3 normal;
        switch (faceIndex)
        {
            default:
                return;
            case 0: // up
                normal = Vector3.Up;
                verts.Add(new Vector3(x, y + 1, z));
                verts.Add(new Vector3(x, y + 1, z + 1));
                verts.Add(new Vector3(x + 1, y + 1, z + 1));
                verts.Add(new Vector3(x + 1, y + 1, z));
                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1));
                uvs.Add(new Vector2(0, 1));
                break;
            case 1: // down
                normal = Vector3.Down;
                verts.Add(new Vector3(x, y, z));
                verts.Add(new Vector3(x + 1, y, z));
                verts.Add(new Vector3(x + 1, y, z + 1));
                verts.Add(new Vector3(x, y, z + 1));
                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(0, 1));
                uvs.Add(new Vector2(1, 1));
                uvs.Add(new Vector2(1, 0));
                break;
            case 2: // left
                normal = Vector3.Left;
                verts.Add(new Vector3(x, y, z));
                verts.Add(new Vector3(x, y, z + 1));
                verts.Add(new Vector3(x, y + 1, z + 1));
                verts.Add(new Vector3(x, y + 1, z));
                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1));
                uvs.Add(new Vector2(0, 1));
                break;
            case 3: // right
                normal = Vector3.Right;
                verts.Add(new Vector3(x + 1, y, z + 1));
                verts.Add(new Vector3(x + 1, y, z));
                verts.Add(new Vector3(x + 1, y + 1, z));
                verts.Add(new Vector3(x + 1, y + 1, z + 1));
                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1));
                uvs.Add(new Vector2(0, 1));
                break;
            case 4: // front
                normal = Vector3.Forward;
                verts.Add(new Vector3(x, y, z + 1));
                verts.Add(new Vector3(x + 1, y, z + 1));
                verts.Add(new Vector3(x + 1, y + 1, z + 1));
                verts.Add(new Vector3(x, y + 1, z + 1));
                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1));
                uvs.Add(new Vector2(0, 1));
                break;
            case 5: // back
                normal = Vector3.Back;
                verts.Add(new Vector3(x + 1, y, z));
                verts.Add(new Vector3(x, y, z));
                verts.Add(new Vector3(x, y + 1, z));
                verts.Add(new Vector3(x + 1, y + 1, z));
                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1));
                uvs.Add(new Vector2(0, 1));
                break;
        }
        colors.Add(light);
        colors.Add(light);
        colors.Add(light);
        colors.Add(light);
        normals.Add(normal);
        normals.Add(normal);
        normals.Add(normal);
        normals.Add(normal);
        AddTriangles(vox.Id);
    }

    private void AddTriangles(int id)
    {
        int vertCount = verts.Count;

        if (!trisLookup.ContainsKey(id))
            trisLookup.Add(id, new List<int>());

        // first triangle
        trisLookup[id].Add(vertCount - 2);
        trisLookup[id].Add(vertCount - 3);
        trisLookup[id].Add(vertCount - 4);

        // second triangle
        trisLookup[id].Add(vertCount - 1);
        trisLookup[id].Add(vertCount - 2);
        trisLookup[id].Add(vertCount - 4);
    }

    public Mesh GetMesh()
    {
        return mesh;
    }

    public ConcavePolygonShape3D GetShape()
    {
        shape.SetFaces(mesh.GetFaces());
        return shape;
    }

    public bool UpdateMesh()
    {
        try
        {
            verts.Clear();
            normals.Clear();
            trisLookup.Clear();
            uvs.Clear();
            colors.Clear();
            AddChunk();
            SurfaceTool st = new SurfaceTool();
            ArrayMesh amesh = new ArrayMesh();
            {
                foreach (var id in trisLookup.Keys)
                {
                    st.Begin(Mesh.PrimitiveType.Triangles);
                    for (int i = 0; i < verts.Count; i++)
                    {
                        st.SetColor(colors[i]);
                        st.SetUV(uvs[i]);
                        st.SetNormal(normals[i]);
                        st.AddVertex(verts[i]);
                    }
                    for (int i = 0; i < trisLookup[id].Count; i++)
                    {
                        st.AddIndex(trisLookup[id][i]);
                    }
                    amesh = st.Commit(amesh);
                }
            }
            Material[] tempList = new Material[trisLookup.Count];
            int i2 = 0;
            foreach (var id in trisLookup.Keys)
            {
                if (id >= 0 && id < world.materials.Count)
                    tempList[i2++] = world.materials[id];
                else
                    i2++;
            }
            materials = tempList;
            for (int i = 0; i < Mathf.Min(materials.Length, amesh.GetSurfaceCount()); i++)
            {
                amesh.SurfaceSetMaterial(i, materials[i]);
            }
            chunk.shouldUpdate = Mathf.Max(chunk.shouldUpdate - 1, 0);

            mesh = amesh;
        }
        finally
        {
        }
        return true;
    }

    public VoxelMesh(VoxelWorld wo, Chunk ch)
    {
        mesh = new ArrayMesh();
        shape = new ConcavePolygonShape3D();
        world = wo;
        chunk = ch;
    }
}