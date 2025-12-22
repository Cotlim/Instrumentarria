using Instrumentarria.MidiReader;
using MeltySynth;
using ReLogic.Content;
using ReLogic.Content.Readers;
using System.Collections.Generic;
using Terraria.ModLoader;

namespace Instrumentarria.Common.Systems.AssetsManagers
{
    /// <summary>
    /// Centralized MIDI asset management system.
    /// Automatically discovers and loads all .mid files from the Assets/Midi/ folder.
    /// </summary>
    internal class MidiAssets : AssetManager<MidiFile>
    {
        protected override string AssetFolder => "Assets/Midi/";
        protected override string FileExtension => ".mid";
        protected override string AssetTypeName => "MIDI";
        protected override IAssetReader AssetReader => new MidiFileReader();
        
        // Backward compatibility - commonly used test file
        public static Asset<MidiFile> TestMidi => GetMidiAsset("Test");

        /// <summary>
        /// Gets a MIDI asset by name (without extension).
        /// </summary>
        public static Asset<MidiFile> GetMidiAsset(string fileName)
        {
            var instance = ModContent.GetInstance<MidiAssets>();
            return instance?.GetAsset(fileName);
        }

        /// <summary>
        /// Gets a MIDI file by name (without extension).
        /// Convenience method that returns the value directly.
        /// </summary>
        public static MidiFile GetMidi(string fileName)
        {
            return GetMidiAsset(fileName)?.Value;
        }

        /// <summary>
        /// Gets all loaded MIDI asset names.
        /// </summary>
        public static IEnumerable<string> GetAllMidiNames()
        {
            var instance = ModContent.GetInstance<MidiAssets>();
            return instance?.GetAllAssetNames() ?? System.Array.Empty<string>();
        }

        /// <summary>
        /// Checks if a MIDI asset with the given name exists.
        /// </summary>
        public static bool HasMidiAsset(string fileName)
        {
            var instance = ModContent.GetInstance<MidiAssets>();
            return instance?.HasAsset(fileName) ?? false;
        }

        protected override void LogAssetLoaded(string fileName, Asset<MidiFile> asset)
        {
            Log.Info($"Loaded MIDI: {fileName} ({asset.Value.Length.TotalSeconds:F1}s)");
        }
    }
}
