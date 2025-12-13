using Instrumentarria.MidiReader;
using MeltySynth;
using Microsoft.Xna.Framework.Audio;
using System;
using Terraria.Audio;

namespace Instrumentarria.Common.Systems.RhythmEngine.Streaming
{
    /// <summary>
    /// Real-time streaming MIDI audio track
    /// Аналог WAVAudioTrack але для MIDI через MeltySynth synthesis
    /// </summary>
    public class MidiStreamingAudioTrack : ASoundEffectBasedAudioTrack
    {
        private readonly IMidiEventProvider _eventProvider;
        private readonly Synthesizer _synthesizer;
        private readonly object _synthLock = new object();
        
        private double _currentTime;
        private System.Diagnostics.Stopwatch _playbackTimer;
        private bool _hasMoreEvents = true;
        private bool _timerStarted = false; // Чи запущений таймер
        private double _bufferDurationSeconds; // Тривалість одного буфера в секундах
        private bool _finishedPlaying = false; // Прапорець завершення

        // Буфери для audio rendering
        private float[] _leftBuffer;
        private float[] _rightBuffer;

        public MidiStreamingAudioTrack(IMidiEventProvider eventProvider, SynthesizerManager synthesizerManager = null)
        {
            if (eventProvider == null)
                throw new ArgumentNullException(nameof(eventProvider));

            _eventProvider = eventProvider;
            
            // Використовуємо singleton manager або створюємо новий
            var manager = synthesizerManager ?? SynthesizerManager.GetInstance();
            
            if (!manager.IsLoaded)
                throw new InvalidOperationException("SynthesizerManager not initialized. Load SoundFont first.");

            _synthesizer = manager.CreateSynthesizer();
            
            // Створюємо DynamicSoundEffectInstance
            CreateSoundEffect(manager.SampleRate, AudioChannels.Stereo);

            _soundEffectInstance.BufferNeeded += OnBufferNeeded;

            // Ініціалізуємо буфери (розмір з базового класу - bufferLength = 4096)
            // 4096 bytes = 2048 samples stereo = 1024 samples per channel
            int samplesPerChannel = bufferLength / 4; // 4 bytes per stereo sample (2 channels * 2 bytes)
            _leftBuffer = new float[samplesPerChannel];
            _rightBuffer = new float[samplesPerChannel];
            
            // Обчислити тривалість буфера
            _bufferDurationSeconds = (double)samplesPerChannel / manager.SampleRate;
            
            _playbackTimer = new System.Diagnostics.Stopwatch();
        }

        public override void PrepareToPlay()
        {
            base.PrepareToPlay();
            
            lock (_synthLock)
            {
                _synthesizer.Reset();
                _eventProvider.Reset();
                _currentTime = 0;
                _hasMoreEvents = true;
                _timerStarted = false; // Скинути прапорець
                _finishedPlaying = false; // Скинути прапорець завершення
                _playbackTimer.Reset(); // НЕ запускати тут!
            }
        }

        // Override Update щоб припинити викликати PrepareBuffer коли закінчено
        public void Update()
        {
            if (!_finishedPlaying && IsPlaying && _soundEffectInstance.PendingBufferCount < 8)
            {
                PrepareBuffer();
            }
        }

        public override void ReadAheadPutAChunkIntoTheBuffer()
        {
            lock (_synthLock)
            {
                // Запустити таймер ТІЛЬКИ коли фактично починається playback
                if (!_timerStarted && _soundEffectInstance.State == SoundState.Playing)
                {
                    _playbackTimer.Restart();
                    _timerStarted = true;
                    _currentTime = 0; // Починаємо з 0
                }
                
                // Обробити MIDI події до поточного часу
                ProcessMidiEvents();

                // Рендерити audio
                _synthesizer.Render(_leftBuffer, _rightBuffer);

                // Конвертувати в PCM16 та застосувати volume boost
                ConvertToPCM16WithBoost(_leftBuffer, _rightBuffer, _bufferToSubmit);

                // Інкрементувати час на тривалість буфера ПЕРЕД перевіркою
                _currentTime += _bufferDurationSeconds;

                // Перевірити чи закінчились події
                bool shouldStop = false;
                if (!_eventProvider.HasMoreEvents && !_hasMoreEvents)
                {
                    // Дамо ще трохи часу для release/затухання звуків (2 секунди після останньої події)
                    if (_eventProvider.TotalDuration.HasValue && 
                        _currentTime > _eventProvider.TotalDuration.Value + 2.0)
                    {
                        shouldStop = true;
                        _finishedPlaying = true; // Встановити прапорець
                    }
                }

                // Подати буфер тільки якщо НЕ зупиняємось
                if (!shouldStop && (_soundEffectInstance.State == SoundState.Playing || _soundEffectInstance.State == SoundState.Paused))
                {
                    _soundEffectInstance.SubmitBuffer(_bufferToSubmit);
                }
                
                // Якщо shouldStop = true, просто не подаємо буфер
                // DynamicSoundEffectInstance автоматично зупиниться коли закінчаться буфери
            }
        }

        private void OnBufferNeeded(object sender, EventArgs e)
        {
            // Якщо закінчили playback, зупинити
            if (_finishedPlaying && _soundEffectInstance.State == SoundState.Playing)
            {
                try
                {
                    _soundEffectInstance.Stop(true); // Immediate stop
                }
                catch { }
            }
        }

        private void ProcessMidiEvents()
        {
            foreach (var (timeInSeconds, midiEvent) in _eventProvider.GetEventsUntil(_currentTime))
            {
                switch (midiEvent)
                {
                    case NoteOnEvent noteOn:
                        _synthesizer.ProcessMidiMessage(noteOn.Channel, 0x90, noteOn.Note, noteOn.Velocity);
                        break;

                    case NoteOffEvent noteOff:
                        _synthesizer.ProcessMidiMessage(noteOff.Channel, 0x80, noteOff.Note, 0);
                        break;

                    case ProgramChangeEvent programChange:
                        _synthesizer.ProcessMidiMessage(programChange.Channel, 0xC0, programChange.Program, 0);
                        break;
                }
            }

            _hasMoreEvents = _eventProvider.HasMoreEvents;
        }

        private void ConvertToPCM16WithBoost(float[] left, float[] right, byte[] output)
        {
            for (int i = 0; i < left.Length; i++)
            {
                // Застосувати volume boost
                float boostedLeft = left[i] * SynthesizerManager.VOLUME_BOOST;
                float boostedRight = right[i] * SynthesizerManager.VOLUME_BOOST;

                // Clamp та конвертувати в PCM16
                short leftSample = (short)(Math.Clamp(boostedLeft, -1.0f, 1.0f) * short.MaxValue);
                short rightSample = (short)(Math.Clamp(boostedRight, -1.0f, 1.0f) * short.MaxValue);

                // Записати в output buffer (interleaved stereo)
                int byteIndex = i * 4;
                output[byteIndex] = (byte)(leftSample & 0xFF);
                output[byteIndex + 1] = (byte)((leftSample >> 8) & 0xFF);
                output[byteIndex + 2] = (byte)(rightSample & 0xFF);
                output[byteIndex + 3] = (byte)((rightSample >> 8) & 0xFF);
            }
        }

        public override void Reuse()
        {
            lock (_synthLock)
            {
                _eventProvider.Reset();
                _synthesizer.Reset();
                _currentTime = 0;
                _playbackTimer.Reset();
            }
        }

        public override void Dispose()
        {
            // Відписатись від event
            if (_soundEffectInstance != null)
            {
                ((DynamicSoundEffectInstance)_soundEffectInstance).BufferNeeded -= OnBufferNeeded;
            }
            
            _playbackTimer?.Stop();
            
            // Dispose SoundEffectInstance безпечно
            if (_soundEffectInstance != null)
            {
                try
                {
                    _soundEffectInstance.Dispose();
                }
                catch { }
            }
            
            // Synthesizer не dispose-имо, бо він може використовуватись іншими tracks
        }

        /// <summary>
        /// Поточний час відтворення в секундах
        /// </summary>
        public double CurrentTime => _currentTime;

        /// <summary>
        /// Чи є ще події для відтворення
        /// </summary>
        public bool HasMoreEvents => _hasMoreEvents;
    }
}
