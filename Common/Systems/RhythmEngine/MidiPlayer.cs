using Instrumentarria.MidiReader;
using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;
using MidiFile = Instrumentarria.MidiReader.MidiFile;
using MeltySynth;

namespace Instrumentarria.Common.Systems.RhythmEngine
{
    /// <summary>
    /// Streaming MIDI player + синтезатор використовуючи MeltySynth
    /// Об'єднує функціонал відтворення MIDI файлів та окремих нот
    /// </summary>
    public class MidiPlayer : ModSystem
    {
        private const int SAMPLE_RATE = 44100;
        private const float VOLUME_BOOST = 8.0f; // Оптимальний boost для гучності (тест показав 3.0 = Peak 0.4, потрібно більше)
        
        private MidiFile _currentMidi;
        private SoundFont _soundFont;
        private Synthesizer _synthesizer;
        
        private List<ScheduledEvent> _scheduledEvents;
        private int _currentEventIndex;
        private double _currentTime;
        private double _bpm = 60.0;
        private int _ticksPerQuarterNote;
        private bool _isPlaying;
        
        // Для точного вимірювання часу
        private System.Diagnostics.Stopwatch _playbackTimer;
        
        // Поточний preset
        private int _currentPresetBank = 0;
        private int _currentPresetNumber = 0;
        
        // Кеш згенерованих нот
        private readonly Dictionary<int, SoundEffect> _noteCache = new();

        private class ScheduledEvent
        {
            public int Tick;
            public double TimeInSeconds;
            public MidiEvent Event;
            public int TrackIndex;
        }

        private class ActiveNote
        {
            public byte Note;
            public byte Velocity;
            public int Channel;
            public double StartTime;
            public SoundEffectInstance Instance;
        }

        // Активні ноти для відстеження NoteOff
        private Dictionary<(int channel, byte note), ActiveNote> _activeNotes = new();

        public bool IsPlaying => _isPlaying;
        public double CurrentTime => _currentTime;
        public double BPM => _bpm;
        public bool IsReady => _synthesizer != null;

        public override void Load()
        {
            _playbackTimer = new System.Diagnostics.Stopwatch();
            InitializeSynthesizer();
        }

        public override void Unload()
        {
            Stop();
            ClearCache();
            _currentMidi = null;
            _scheduledEvents = null;
            _synthesizer = null;
            _soundFont = null;
        }

        /// <summary>
        /// Ініціалізує MeltySynth synthesizer
        /// </summary>
        private void InitializeSynthesizer()
        {
            try
            {
                Mod.Logger.Info("Loading SoundFont from mod resources...");
                
                // MeltySynth потребує seekable stream, тому копіюємо в MemoryStream
                using (var compressedStream = Mod.GetFileStream("Assets/SoundFonts/default.sf2"))
                {
                    if (compressedStream == null)
                    {
                        Mod.Logger.Error("Could not find SoundFont file in mod resources");
                        return;
                    }
                    
                    // Копіювати в MemoryStream (seekable)
                    using (var memoryStream = new System.IO.MemoryStream())
                    {
                        compressedStream.CopyTo(memoryStream);
                        memoryStream.Position = 0; // Reset до початку
                        
                        Mod.Logger.Debug($"SoundFont loaded into memory: {memoryStream.Length} bytes");
                        
                        _soundFont = new SoundFont(memoryStream);
                    }
                }
                
                var settings = new SynthesizerSettings(SAMPLE_RATE);
                _synthesizer = new Synthesizer(_soundFont, settings);

                Mod.Logger.Info($"MidiPlayer initialized with {_soundFont.Presets.Count} presets");
            }
            catch (Exception ex)
            {
                Mod.Logger.Error($"Failed to initialize synthesizer: {ex.Message}");
                Mod.Logger.Error($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Гарантує що synthesizer ініціалізований
        /// </summary>
        private void EnsureInitialized()
        {
            if (_synthesizer == null)
            {
                InitializeSynthesizer();
            }
        }

        /// <summary>
        /// Завантажує MIDI файл
        /// </summary>
        public void LoadMidi(MidiFile midiFile)
        {
            Stop();

            _currentMidi = midiFile;
            _ticksPerQuarterNote = midiFile.TicksPerQuarterNote;

            // Витягнути темп
            ExtractTempo();

            // Створити timeline з абсолютним часом
            _scheduledEvents = FlattenTracksWithTime(midiFile);

            Mod.Logger.Info($"Loaded MIDI: {_scheduledEvents.Count} events, BPM: {_bpm}, TPQN: {_ticksPerQuarterNote}");
            
            // Debug: показати перші кілька подій
            for (int i = 0; i < Math.Min(5, _scheduledEvents.Count); i++)
            {
                var evt = _scheduledEvents[i];
                Mod.Logger.Debug($"Event {i}: Tick={evt.Tick}, Time={evt.TimeInSeconds:F3}s, Type={evt.Event.GetType().Name}");
            }
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

            EnsureInitialized();

            if (_synthesizer == null)
            {
                Mod.Logger.Error("Synthesizer not ready!");
                return;
            }

            _isPlaying = true;
            _currentTime = 0.0;
            _currentEventIndex = 0;

            // Reset та запустити таймер для точного вимірювання часу
            _playbackTimer.Restart();

            // Reset synthesizer
            _synthesizer.Reset();

            Mod.Logger.Info("Streaming MIDI playback started");
        }

        // ============================================
        // Методи для роботи з окремими нотами
        // ============================================

        /// <summary>
        /// Відтворює окрему ноту з автоматичною SF2 обробкою
        /// </summary>
        public void PlayNote(byte midiNote, byte velocity, float volume = 1.0f, float durationSeconds = 2.0f)
        {
            EnsureInitialized();

            if (_synthesizer == null)
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
                    // MeltySynth вже врахував velocity, тут тільки додатковий volume control
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
            // Створити ключ для кешування
            int cacheKey = (midiNote << 16) | (velocity << 8) | (int)(durationSeconds * 10);
            
            if (_noteCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            try
            {
                int sampleCount = (int)(SAMPLE_RATE * durationSeconds);
                float[] left = new float[sampleCount];
                float[] right = new float[sampleCount];

                // Вибрати preset
                _synthesizer.ProcessMidiMessage(0, 0xC0, _currentPresetNumber, 0);

                // Note On
                _synthesizer.ProcessMidiMessage(0, 0x90, midiNote, velocity);
                
                // Render audio
                _synthesizer.Render(left, right);
                
                // Note Off
                _synthesizer.ProcessMidiMessage(0, 0x80, midiNote, 0);

                // Конвертувати в stereo PCM16 БЕЗ додаткового volume multiplier
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
            byte[] pcm16 = new byte[left.Length * 4]; // 2 канали * 2 байти
            
            for (int i = 0; i < left.Length; i++)
            {
                // Застосувати VOLUME_BOOST
                float boostedLeft = left[i] * VOLUME_BOOST;
                float boostedRight = right[i] * VOLUME_BOOST;
                
                // Конвертація з boost та clamp
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
            EnsureInitialized();

            if (_soundFont == null)
                return Array.Empty<(int, int, string)>();

            var instruments = new List<(int, int, string)>();
            
            foreach (var preset in _soundFont.Presets)
            {
                instruments.Add((preset.BankNumber, preset.PatchNumber, preset.Name));
            }
            
            return instruments.ToArray();
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
            EnsureInitialized();

            if (_synthesizer == null)
                return "Synthesizer: Not initialized";

            return $"StreamingMidiPlayer: Ready\n" +
                   $"Presets: {_soundFont?.Presets.Count ?? 0}\n" +
                   $"Current: Bank={_currentPresetBank}, Program={_currentPresetNumber}\n" +
                   $"Sample Rate: {SAMPLE_RATE}Hz\n" +
                   $"Cached Notes: {_noteCache.Count}";
        }

        /// <summary>
        /// Зупиняє відтворення
        /// </summary>
        public void Stop()
        {
            _isPlaying = false;
            _currentTime = 0.0;
            _currentEventIndex = 0;
            
            // Зупинити таймер
            _playbackTimer?.Stop();
            
            // Зупинити всі активні ноти
            foreach (var activeNote in _activeNotes.Values)
            {
                activeNote.Instance?.Stop();
                activeNote.Instance?.Dispose();
            }
            _activeNotes.Clear();
        }

        public override void PostUpdateEverything()
        {
            if (!_isPlaying || _synthesizer == null)
                return;

            // Використати реальний час з Stopwatch (найточніше)
            _currentTime = _playbackTimer.Elapsed.TotalSeconds;

            // Debug: порівняти з FPS-based timing
            // float deltaTime = 1.0f / Math.Max(Main.frameRate, 1);
            // Mod.Logger.Debug($"Stopwatch: {_currentTime:F3}s vs FPS: {deltaTime:F3}s");

            // Обробити MIDI події які настав час
            ProcessPendingEvents();

            // Перевірити чи досягли кінця
            if (_currentEventIndex >= _scheduledEvents.Count)
            {
                // Дочекатися поки все відіграє (2 секунди після останньої події)
                if (_currentTime > _scheduledEvents.Last().TimeInSeconds + 2.0)
                {
                    Stop();
                    Mod.Logger.Info($"MIDI playback finished. Total time: {_currentTime:F2}s, FPS: {Main.frameRate}");
                }
            }
        }

        /// <summary>
        /// Обробляє MIDI події що мають відбутися зараз
        /// </summary>
        private void ProcessPendingEvents()
        {
            // Обробити всі події що мають відбутися до поточного часу
            while (_currentEventIndex < _scheduledEvents.Count)
            {
                var scheduled = _scheduledEvents[_currentEventIndex];
                
                if (scheduled.TimeInSeconds > _currentTime)
                    break; // Ще не час

                // Обробити подію - тепер з pre-rendering для нот
                ProcessMidiEventWithPreRender(scheduled);
                
                _currentEventIndex++;
            }
        }

        /// <summary>
        /// Обробляє MIDI подію з pre-rendering нот для усунення шуму
        /// </summary>
        private void ProcessMidiEventWithPreRender(ScheduledEvent scheduled)
        {
            switch (scheduled.Event)
            {
                case NoteOnEvent noteOn when noteOn.Velocity > 0:
                    // Знайти тривалість ноти (NoteOff)
                    double noteDuration = FindNoteDuration(scheduled, noteOn);
                    
                    if (noteDuration > 0)
                    {
                        // Pre-render та відтворити ноту
                        PlayPreRenderedNote(noteOn.Note, noteOn.Velocity, noteOn.Channel, noteDuration);
                    }
                    break;

                case NoteOffEvent noteOff:
                    // NoteOff вже оброблений в NoteOn, нічого не робимо
                    break;

                case NoteOnEvent noteOn when noteOn.Velocity == 0:
                    // NoteOn з velocity 0 = NoteOff, ігноруємо
                    break;

                case TempoChangeEvent tempo:
                    // Темп вже враховано в FlattenTracksWithTime, тут тільки для логування
                    _bpm = tempo.Bpm;
                    break;
            }
        }

        /// <summary>
        /// Знаходить тривалість ноти, шукаючи відповідний NoteOff
        /// </summary>
        private double FindNoteDuration(ScheduledEvent noteOnEvent, NoteOnEvent noteOn)
        {
            // Шукаємо NoteOff для цієї ноти та каналу
            for (int i = _currentEventIndex + 1; i < _scheduledEvents.Count; i++)
            {
                var evt = _scheduledEvents[i];
                
                // Перевіряємо NoteOff
                if (evt.Event is NoteOffEvent noteOff && 
                    noteOff.Note == noteOn.Note && 
                    noteOff.Channel == noteOn.Channel)
                {
                    return evt.TimeInSeconds - noteOnEvent.TimeInSeconds;
                }
                
                // Перевіряємо NoteOn з velocity 0 (альтернативний NoteOff)
                if (evt.Event is NoteOnEvent altNoteOff && 
                    altNoteOff.Velocity == 0 && 
                    altNoteOff.Note == noteOn.Note && 
                    altNoteOff.Channel == noteOn.Channel)
                {
                    return evt.TimeInSeconds - noteOnEvent.TimeInSeconds;
                }
            }
            
            // Якщо NoteOff не знайдено, використати дефолтну тривалість
            Mod.Logger.Warn($"NoteOff not found for note {noteOn.Note}, channel {noteOn.Channel}. Using default duration.");
            return 1.0; // 1 секунда за замовчуванням
        }

        /// <summary>
        /// Відтворює pre-rendered ноту з точною тривалістю
        /// </summary>
        private void PlayPreRenderedNote(byte note, byte velocity, int channel, double durationSeconds)
        {
            try
            {
                // Обмежити максимальну тривалість для уникнення занадто великих буферів
                float clampedDuration = (float)Math.Min(durationSeconds, 10.0);
                
                // Генерувати ноту з точною тривалістю
                var sound = GeneratePreRenderedNote(note, velocity, channel, clampedDuration);
                
                if (sound != null)
                {
                    var instance = sound.CreateInstance();
                    // Застосувати VOLUME_BOOST для збільшення гучності
                    instance.Volume = Math.Min(1.0f, VOLUME_BOOST);
                    instance.Play();
                    
                    // Зберегти instance для можливості зупинки
                    var key = (channel, note);
                    if (_activeNotes.ContainsKey(key))
                    {
                        _activeNotes[key].Instance?.Stop();
                        _activeNotes[key].Instance?.Dispose();
                    }
                    
                    _activeNotes[key] = new ActiveNote
                    {
                        Note = note,
                        Velocity = velocity,
                        Channel = channel,
                        StartTime = _currentTime,
                        Instance = instance
                    };
                }
            }
            catch (Exception ex)
            {
                Mod.Logger.Error($"Error playing pre-rendered note: {ex.Message}");
            }
        }

        /// <summary>
        /// Генерує pre-rendered SoundEffect для ноти з точною тривалістю
        /// </summary>
        private SoundEffect GeneratePreRenderedNote(byte note, byte velocity, int channel, float durationSeconds)
        {
            // Додати release tail (0.5 секунди) для природного затухання
            const float RELEASE_TAIL = 3f;
            float totalDuration = durationSeconds + RELEASE_TAIL;
            
            // Створити ключ для кешування (включає channel)
            int cacheKey = (channel << 24) | (note << 16) | (velocity << 8) | (int)(durationSeconds * 10);
            
            if (_noteCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            try
            {
                int sampleCount = (int)(SAMPLE_RATE * totalDuration);
                int noteOffSample = (int)(SAMPLE_RATE * durationSeconds);
                
                float[] left = new float[sampleCount];
                float[] right = new float[sampleCount];

                // Вибрати preset для каналу
                _synthesizer.ProcessMidiMessage(channel, 0xC0, _currentPresetNumber, 0);

                // Note On
                _synthesizer.ProcessMidiMessage(channel, 0x90, note, velocity);
                
                // Render до NoteOff
                _synthesizer.Render(left.AsSpan(0, noteOffSample), right.AsSpan(0, noteOffSample));
                
                // Note Off - тепер нота затухає природно
                _synthesizer.ProcessMidiMessage(channel, 0x80, note, 0);
                
                // Render release tail (затухання після NoteOff)
                int releaseSamples = sampleCount - noteOffSample;
                _synthesizer.Render(left.AsSpan(noteOffSample, releaseSamples), right.AsSpan(noteOffSample, releaseSamples));

                // Конвертувати в stereo PCM16
                byte[] pcm16 = ConvertToStereoPCM16(left, right);

                var soundEffect = new SoundEffect(pcm16, SAMPLE_RATE, AudioChannels.Stereo);
                
                // Кешувати для повторного використання
                _noteCache[cacheKey] = soundEffect;
                
                return soundEffect;
            }
            catch (Exception ex)
            {
                Mod.Logger.Error($"Error generating pre-rendered note: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Витягує темп з MIDI
        /// </summary>
        private void ExtractTempo()
        {
            if (_currentMidi.Tracks.Length == 0)
                return;

            var tempoEvent = _currentMidi.Tracks[0].MidiEvents
                .OfType<TempoChangeEvent>()
                .FirstOrDefault();

            _bpm = tempoEvent?.Bpm ?? 120.0;
        }

        /// <summary>
        /// Згортає треки в timeline з абсолютним часом
        /// </summary>
        private List<ScheduledEvent> FlattenTracksWithTime(MidiFile midi)
        {
            var events = new List<ScheduledEvent>();
            
            // Зібрати всі події з усіх треків
            var allEvents = new List<(int tick, int trackIndex, MidiEvent evt)>();
            
            for (int trackIndex = 0; trackIndex < midi.Tracks.Length; trackIndex++)
            {
                var track = midi.Tracks[trackIndex];
                foreach (var evt in track.MidiEvents)
                {
                    allEvents.Add((evt.Time, trackIndex, evt));
                }
            }
            
            // Сортувати за тіком
            allEvents.Sort((a, b) => a.tick.CompareTo(b.tick));
            
            // Обчислити час для кожної події, враховуючи зміни темпу
            double currentBpm = _bpm; // Початковий BPM
            double currentTime = 0.0;
            int lastTick = 0;
            
            foreach (var (tick, trackIndex, evt) in allEvents)
            {
                // Обчислити час від останнього тіку до поточного
                int deltaTicks = tick - lastTick;
                if (deltaTicks > 0)
                {
                    double secondsPerTick = 60.0 / (currentBpm * _ticksPerQuarterNote);
                    currentTime += deltaTicks * secondsPerTick;
                }
                
                // Перевірити чи це TempoChangeEvent
                if (evt is TempoChangeEvent tempoChange)
                {
                    currentBpm = tempoChange.Bpm;
                    Mod.Logger.Debug($"Tempo change at tick {tick}: {currentBpm:F1} BPM, time: {currentTime:F3}s");
                }
                
                events.Add(new ScheduledEvent
                {
                    Tick = tick,
                    TimeInSeconds = currentTime,
                    Event = evt,
                    TrackIndex = trackIndex
                });
                
                lastTick = tick;
            }
            
            return events;
        }
    }
}
