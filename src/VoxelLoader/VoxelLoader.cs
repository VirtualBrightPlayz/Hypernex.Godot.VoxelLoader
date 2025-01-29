using System.Collections.Generic;
using System.Text;
using Godot;
using Hypernex.CCK.GodotVersion.Classes;
using SharpNBT;

namespace Hypernex.CCK.GodotVersion
{
    public partial class VoxelLoader : ISceneProvider
    {
        public void Dispose()
        {
        }

        public PackedScene LoadFromFile(string filePath)
        {
            Error err = Error.Ok;
            ZipReader reader = new ZipReader();
            err = reader.Open(filePath);
            if (err != Error.Ok)
                return null;
            FileVoxelWorld world = new FileVoxelWorld();
            world.worldFile = reader.ReadFile("world.txt");
            world.blocksFile = reader.ReadFile("textures.zip");
            //
            VoxelWorldRenderer renderer = new VoxelWorldRenderer();
            world.AddChild(renderer);
            renderer.Owner = world;
            renderer.world = world;
            //
            WorldDescriptor desc = new WorldDescriptor();
            desc.Position = Vector3.One * 30f;
            desc.StartPositions = new NodePath[] { "." };
            world.AddChild(desc);
            desc.Owner = world;
            //
            DirectionalLight3D light = new DirectionalLight3D();
            world.AddChild(light);
            light.ShadowEnabled = true;
            light.LookAtFromPosition(Vector3.One, Vector3.Down);
            light.Owner = world;
            //
            WorldEnvironment env = new WorldEnvironment();
            world.AddChild(env);
            env.Owner = world;
            env.Environment = new Godot.Environment()
            {
                BackgroundMode = Godot.Environment.BGMode.Sky,
                Sky = new Sky()
                {
                    SkyMaterial = new ProceduralSkyMaterial(),
                },
            };
            //
            PackedScene scene = new PackedScene();
            err = scene.Pack(world);
            if (err != Error.Ok)
                return null;
            return scene;
        }
    }
}