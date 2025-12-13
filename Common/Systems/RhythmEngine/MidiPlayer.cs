using Instrumentarria.MidiReader;
using Instrumentarria.Common.Systems.RhythmEngine.Streaming;
using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;
using MidiFile = Instrumentarria.MidiReader.MidiFile;

namespace Instrumentarria.Common.Systems.RhythmEngine
{
    /// <summary>
    /// High-level MIDI player ModSystem
    /// Використовує MidiStreamingAudioTrack для відтворення
    /// </summary>
    public class MidiPlayer : ModSystem
    {
        private const int SAMPLE_RATE = 44100;
        
        private MidiFile _currentMidi;
        private MidiStreamingAudioTrack _currentTrack;
        private MidiFileEventProvider _currentEventProvider;
        private SynthesizerManager _synthesizerManager;
        
        // Поточний preset для окремих нот
        private int _currentPresetBank = 0;
        private int _currentPresetNumber = 0;
        
        // Кеш згенерованих нот (тільки для окремих нот, не для MIDI playback)
        private readonly Dictionary<int, SoundEffect> _noteCache = new();
        
        // Кеш event providers для швидкого перезапуску
        private readonly Dictionary<string, MidiFileEventProvider> _eventProviderCache = new();


        public bool IsPlaying => _currentTrack?.IsPlaying ?? false;
        public double CurrentTime => _currentTrack?.CurrentTime ?? 0.0;
        public double BPM => _currentEventProvider?.InitialBPM ?? 120.0;
        public bool IsReady => _synthesizerManager?.IsLoaded ?? false;

        public override void Load()
        {
            _synthesizerManager = SynthesizerManager.GetInstance(SAMPLE_RATE);
            _synthesizerManager.LoadSoundFont(Mod);
        }

        public override void Unload()
        {
            // НЕ викликаємо Stop() тут, бо це може бути з background thread
            // Просто очищаємо ресурси
            ClearCache();
            _eventProviderCache.Clear();
            _currentMidi = null;
            
            // Dispose track без Stop - це безпечно
            if (_currentTrack != null)
            {
                try
                {
                    _currentTrack.Dispose();
                }
                catch { }
                _currentTrack = null;
            }
            
            _currentEventProvider = null;
            SynthesizerManager.Reset();
        }


        /// <summary>
        /// Завантажує MIDI файл
        /// </summary>
        public void LoadMidi(MidiFile midiFile)
        {
            Stop();

            _currentMidi = midiFile;

            // Використовуємо кешований event provider якщо можливо
            string cacheKey = midiFile.GetHashCode().ToString();
            
            if (!_eventProviderCache.TryGetValue(cacheKey, out _currentEventProvider))
            {
                _currentEventProvider = new MidiFileEventProvider(midiFile);
                _eventProviderCache[cacheKey] = _currentEventProvider;
            }
            else
            {
                _currentEventProvider.Reset();
            }

            // Створити новий track
            _currentTrack?.Dispose();
            _currentTrack = new MidiStreamingAudioTrack(_currentEventProvider, _synthesizerManager);

            Mod.Logger.Info($"Loaded MIDI file, duration: {_currentEventProvider.TotalDuration:F2}s, BPM: {_currentEventProvider.InitialBPM:F1}");
        }

        /// <summary>
        /// Починає відтворення MIDI
        /// </summary>
        public void Play()
        {
            if (_currentMidi == null)
            {
                Mod.Logger.Warn("No MIDI loaded");
                return;
            }

            if (_currentTrack == null)
            {
                Mod.Logger.Error("Track not ready!");
                return;
            }

            _currentTrack.Play();
            Mod.Logger.Info("Streaming MIDI playback started");
        }

        /// <summary>
        /// Зупиняє відтворення
        /// </summary>
        public void Stop()
        {
            _currentTrack?.Stop(Microsoft.Xna.Framework.Audio.AudioStopOptions.Immediate);
        }

        /// <summary>
        /// Пауза
        /// </summary>
        public void Pause()
        {
            _currentTrack?.Pause();
        }

        /// <summary>
        /// Відновити після паузи
        /// </summary>
        public void Resume()
        {
            _currentTrack?.Resume();
        }

        // ============================================
        // Методи для роботи з окремими нотами
        // ============================================

        /// <summary>
        /// Відтворює окрему ноту з автоматичною SF2 обробкою
        /// </summary>
        public void PlayNote(byte midiNote, byte velocity, float volume = 1.0f, float durationSeconds = 2.0f)
        {
            if (!_synthesizerManager.IsLoaded)
            {
                Mod.Logger.Debug("Synthesizer not ready");
                return;
            }

            try
            {
                var sound = GenerateSingleNote(midiNote, velocity, durationSeconds);
                
                if (sound != null)
                {
                    var instance = sound.CreateInstance();
                    instance.Volume = volume;
                    instance.Play();
                }
            }
            catch (Exception ex)
            {
                Mod.Logger.Error($"Error playing note: {ex.Message}");
            }
        }

        /// <summary>
        /// Генерує SoundEffect для окремої ноти
        /// </summary>
        private SoundEffect GenerateSingleNote(byte midiNote, byte velocity, float durationSeconds)
        {
            int cacheKey = (midiNote << 16) | (velocity << 8) | (int)(durationSeconds * 10);
            
            if (_noteCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            try
            {
                var synthesizer = _synthesizerManager.CreateSynthesizer();
                
                int sampleCount = (int)(SAMPLE_RATE * durationSeconds);
                float[] left = new float[sampleCount];
                float[] right = new float[sampleCount];

                synthesizer.ProcessMidiMessage(0, 0xC0, _currentPresetNumber, 0);
                synthesizer.ProcessMidiMessage(0, 0x90, midiNote, velocity);
                synthesizer.Render(left, right);
                synthesizer.ProcessMidiMessage(0, 0x80, midiNote, 0);

                byte[] pcm16 = ConvertToStereoPCM16(left, right);
                var soundEffect = new SoundEffect(pcm16, SAMPLE_RATE, AudioChannels.Stereo);
                
                _noteCache[cacheKey] = soundEffect;
                
                return soundEffect;
            }
            catch (Exception ex)
            {
                Mod.Logger.Error($"Error generating note: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Конвертує float буфери в stereo PCM16
        /// </summary>
        private byte[] ConvertToStereoPCM16(float[] left, float[] right)
        {
            byte[] pcm16 = new byte[left.Length * 4];
            
            for (int i = 0; i < left.Length; i++)
            {
                float boostedLeft = left[i] * SynthesizerManager.VOLUME_BOOST;
                float boostedRight = right[i] * SynthesizerManager.VOLUME_BOOST;
                
                short leftSample = (short)(Math.Clamp(boostedLeft, -1.0f, 1.0f) * short.MaxValue);
                short rightSample = (short)(Math.Clamp(boostedRight, -1.0f, 1.0f) * short.MaxValue);
                
                pcm16[i * 4] = (byte)(leftSample & 0xFF);
                pcm16[i * 4 + 1] = (byte)((leftSample >> 8) & 0xFF);
                pcm16[i * 4 + 2] = (byte)(rightSample & 0xFF);
                pcm16[i * 4 + 3] = (byte)((rightSample >> 8) & 0xFF);
            }
            
            return pcm16;
        }

        /// <summary>
        /// Встановлює інструмент (preset)
        /// </summary>
        public void SetInstrument(int bank, int program)
        {
            _currentPresetBank = bank;
            _currentPresetNumber = program;
            ClearCache();
            
            Mod.Logger.Info($"Switched to Bank={bank}, Program={program}");
        }

        /// <summary>
        /// Отримати список доступних інструментів
        /// </summary>
        public (int bank, int program, string name)[] GetAvailableInstruments()
        {
            return _synthesizerManager?.GetAvailableInstruments() ?? Array.Empty<(int, int, string)>();
        }

        /// <summary>
        /// Очищає кеш нот
        /// </summary>
        private void ClearCache()
        {
            foreach (var sound in _noteCache.Values)
            {
                sound?.Dispose();
            }
            _noteCache.Clear();
        }

        /// <summary>
        /// Отримати інформацію
        /// </summary>
        public string GetInfo()
        {
            if (!_synthesizerManager.IsLoaded)
                return "Synthesizer: Not initialized";

            var instruments = _synthesizerManager.GetAvailableInstruments();
            return $"StreamingMidiPlayer: Ready\n" +
                   $"Presets: {instruments.Length}\n" +
                   $"Current: Bank={_currentPresetBank}, Program={_currentPresetNumber}\n" +
                   $"Sample Rate: {SAMPLE_RATE}Hz\n" +
                   $"Cached Notes: {_noteCache.Count}\n" +
                   $"IsPlaying: {IsPlaying}";
        }

        public override void PostUpdateEverything()
        {
            // Track автоматично оновлюється через базовий клас Update()
            _currentTrack?.Update();
        }
    }
}
