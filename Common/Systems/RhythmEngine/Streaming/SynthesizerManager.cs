using MeltySynth;
using System;
using System.IO;
using Terraria.ModLoader;

namespace Instrumentarria.Common.Systems.RhythmEngine.Streaming
{
    /// <summary>
    /// Управляє SoundFont та Synthesizer для MIDI відтворення
    /// Thread-safe singleton для використання в різних треках
    /// </summary>
    public class SynthesizerManager : IDisposable
    {
        private static SynthesizerManager _instance;
        private static readonly object _lock = new object();

        private SoundFont _soundFont;
        private readonly int _sampleRate;
        private bool _disposed;

        public const int DEFAULT_SAMPLE_RATE = 44100;
        public const float VOLUME_BOOST = 8.0f;

        private SynthesizerManager(int sampleRate)
        {
            _sampleRate = sampleRate;
        }

        /// <summary>
        /// Отримує або створює singleton instance
        /// </summary>
        public static SynthesizerManager GetInstance(int sampleRate = DEFAULT_SAMPLE_RATE)
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new SynthesizerManager(sampleRate);
                    }
                }
            }
            return _instance;
        }

        /// <summary>
        /// Завантажує SoundFont з mod resources
        /// </summary>
        public void LoadSoundFont(Mod mod, string soundFontPath = "Assets/SoundFonts/default.sf2")
        {
            if (_soundFont != null)
                return; // Вже завантажено

            lock (_lock)
            {
                if (_soundFont != null)
                    return;

                try
                {
                    mod.Logger.Info($"Loading SoundFont from {soundFontPath}...");

                    using (var compressedStream = mod.GetFileStream(soundFontPath, true))
                    {
                        if (compressedStream == null)
                        {
                            throw new FileNotFoundException($"Could not find SoundFont at {soundFontPath}");
                        }

                        // MeltySynth потребує seekable stream
                        using (var memoryStream = new MemoryStream())
                        {
                            compressedStream.CopyTo(memoryStream);
                            memoryStream.Position = 0;

                            mod.Logger.Debug($"SoundFont loaded into memory: {memoryStream.Length} bytes");

                            _soundFont = new SoundFont(memoryStream);
                            mod.Logger.Info($"SoundFont initialized with {_soundFont.Presets.Count} presets");
                        }
                    }
                }
                catch (Exception ex)
                {
                    mod.Logger.Error($"Failed to load SoundFont: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Створює новий Synthesizer instance
        /// </summary>
        public Synthesizer CreateSynthesizer()
        {
            if (_soundFont == null)
                throw new InvalidOperationException("SoundFont not loaded. Call LoadSoundFont first.");

            var settings = new SynthesizerSettings(_sampleRate);
            return new Synthesizer(_soundFont, settings);
        }

        /// <summary>
        /// Отримує список доступних інструментів
        /// </summary>
        public (int bank, int program, string name)[] GetAvailableInstruments()
        {
            if (_soundFont == null)
                return Array.Empty<(int, int, string)>();

            var instruments = new System.Collections.Generic.List<(int, int, string)>();

            foreach (var preset in _soundFont.Presets)
            {
                instruments.Add((preset.BankNumber, preset.PatchNumber, preset.Name));
            }

            return instruments.ToArray();
        }

        public int SampleRate => _sampleRate;
        public bool IsLoaded => _soundFont != null;

        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_lock)
            {
                _soundFont = null;
                _disposed = true;
            }
        }

        /// <summary>
        /// Скидає singleton (для unload)
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _instance?.Dispose();
                _instance = null;
            }
        }
    }
}
