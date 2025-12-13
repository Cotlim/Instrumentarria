using Instrumentarria.Common.Systems.CustomAudioSystem;
using ReLogic.Content;
using ReLogic.Content.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.ModLoader;

namespace Instrumentarria.MidiReader
{
    internal class MidiAssets : ModSystem
    {
        public static Asset<MidiFile> TestMidi;

        private static List<Stream> _openedStreams;
        public override void Load()
        {
            _openedStreams = new();

            string fileName = "Assets/Test.mid";
            var stream = ModContent.GetInstance<Instrumentarria>().GetFileStream(fileName, newFileStream: true);
            TestMidi = CreateUntrackedWithReader<MidiFile>(stream, fileName, new MidiFileReader());
            _openedStreams.Add(stream);
        }

        public void CloseStreams()
        {
            TestMidi.Dispose();
            _openedStreams.ForEach(s => s.Dispose());
            _openedStreams.Clear();
            Log.Info("MidiAssets Unloaded");
        }

        public Asset<T> CreateUntrackedWithReader<T>(Stream stream, string name, IAssetReader reader, AssetRequestMode mode = AssetRequestMode.ImmediateLoad) where T : class
        {
            if (Main.Assets is not AssetRepository)
            {
                return null;
            }
            var self = Main.Assets as AssetRepository;

            var ext = Path.GetExtension(name);
            var asset = new Asset<T>(name[..^ext.Length]);
            var loadTask = self.LoadUntracked(stream, reader, asset, mode);
            asset.Wait = () => self.SafelyWaitForLoad(asset, loadTask, tracked: false);
            if (mode == AssetRequestMode.ImmediateLoad)
                asset.Wait();

            return asset;
        }
    }
}
