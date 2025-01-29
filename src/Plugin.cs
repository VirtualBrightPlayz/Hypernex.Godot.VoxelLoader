using System.IO;
using System.Text;
using Godot;
using Hypernex.CCK;
using Hypernex.CCK.GodotVersion;

namespace Hypernex.GodotVersion.VoxelProvider
{
    public class Plugin : HypernexPlugin
    {
        public Plugin Instance { get; private set; }

        public override string PluginName => "VoxelSceneProvider";

        public override string PluginCreator => "VirtualBrightPlayz";

        public override string PluginVersion => "0.0.0.0";

        public override void OnPluginLoaded()
        {
            Instance = this;
            Init.WorldProvider = VoxelProvider;
        }

        private ISceneProvider VoxelProvider()
        {
            return new VoxelLoader();
        }

        public static string GetItemsTsv()
        {
            var stream = typeof(Plugin).Assembly.GetManifestResourceStream($"Hypernex.Godot.VoxelLoader.assets.items.tsv");
            byte[] data = new byte[stream.Length];
            stream.Read(data);
            return Encoding.UTF8.GetString(data);
        }

        public static T GetBuiltinAsset<T>(string name) where T : Resource
        {
            return GetBuiltinAsset(name, typeof(T).Name) as T;
        }

        public static Resource GetBuiltinAsset(string name, string type)
        {
            string path = Path.Combine(OS.GetUserDataDir(), name);
            using var fs = File.OpenWrite(path);
            var stream = typeof(Plugin).Assembly.GetManifestResourceStream($"Hypernex.Godot.VoxelLoader.assets.{name}");
            stream.CopyTo(fs);
            fs.Flush();
            fs.Close();
            fs.Dispose();
            return ResourceLoader.Load(path, type);
        }
    }
}