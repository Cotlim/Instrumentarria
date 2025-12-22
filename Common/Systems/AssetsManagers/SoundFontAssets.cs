using Instrumentarria.MidiReader;
using MeltySynth;
using ReLogic.Content;
using ReLogic.Content.Readers;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader;

namespace Instrumentarria.Common.Systems.AssetsManagers
{
    /// <summary>
    /// Centralized SoundFont asset management system.
    /// Automatically discovers and loads all .sf2 files from the Assets/SoundFonts/ folder.
    /// Provides cached SoundFont instances as Asset&lt;SoundFont&gt; for use by multiple synthesizers.
    /// </summary>
    internal class SoundFontAssets : AssetManager<SoundFont>
    {
        protected override string AssetFolder => "Assets/SoundFonts/";
        protected override string FileExtension => ".sf2";
        protected override string AssetTypeName => "SoundFont";
        protected override IAssetReader AssetReader => new SoundFontReader();
        
        // Default SoundFont for backward compatibility
        public static SoundFont DefaultSoundFont => GetSoundFont("default");

        /// <summary>
        /// Gets a SoundFont asset by name (without extension).
        /// </summary>
        public static Asset<SoundFont> GetSoundFontAsset(string fileName)
        {
            var instance = ModContent.GetInstance<SoundFontAssets>();
            return instance?.GetAsset(fileName);
        }

        /// <summary>
        /// Gets a SoundFont by name (without extension).
        /// Convenience method that returns the value directly.
        /// </summary>
        public static SoundFont GetSoundFont(string fileName)
        {
            return GetSoundFontAsset(fileName)?.Value;
        }

        /// <summary>
        /// Gets all loaded SoundFont names.
        /// </summary>
        public static IEnumerable<string> GetAllSoundFontNames()
        {
            var instance = ModContent.GetInstance<SoundFontAssets>();
            return instance?.GetAllAssetNames() ?? Array.Empty<string>();
        }

        /// <summary>
        /// Checks if a SoundFont with the given name exists.
        /// </summary>
        public static bool HasSoundFont(string fileName)
        {
            var instance = ModContent.GetInstance<SoundFontAssets>();
            return instance?.HasAsset(fileName) ?? false;
        }

        /// <summary>
        /// Gets available instruments from a specific SoundFont.
        /// </summary>
        public static (int bank, int program, string name)[] GetInstruments(string soundFontName)
        {
            var soundFont = GetSoundFont(soundFontName);
            if (soundFont == null)
                return Array.Empty<(int, int, string)>();

            var instruments = new List<(int, int, string)>();

            foreach (var preset in soundFont.Presets)
            {
                instruments.Add((preset.BankNumber, preset.PatchNumber, preset.Name));
            }

            return instruments.ToArray();
        }

        protected override void LogAssetLoaded(string fileName, Asset<SoundFont> asset)
        {
            Log.Info($"Loaded SoundFont: {fileName} ({asset.Value.Presets.Count} presets)");
        }
    }
}

