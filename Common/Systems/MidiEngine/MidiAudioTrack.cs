using MeltySynth;
using Microsoft.Xna.Framework.Audio;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace Instrumentarria.Common.Systems.MidiEngine
{
    /// <summary>
    /// Real-time streaming MIDI audio track.
    /// Coordinates MIDI event processing, audio synthesis, and buffer management.
    /// Each instance has its own Synthesizer and can use different SoundFonts.
    /// </summary>
    public class MidiAudioTrack : ASoundEffectBasedAudioTrack
    {
        private readonly MidiPlayer _midiPlayer;

        // Playback state
        private double _currentTime;
        private readonly double _bufferDurationSeconds;

        // Sub-buffer processing for accurate MIDI event timing
        private int _subBufferDivisor = 16; // Default: process events every 256 samples (~5.8ms at 44100Hz)

        // Playback control - synchronization with another audio track
        private AudioTrackSynchronizer _synchronizer;

        // Audio rendering buffers
        private readonly float[] _leftBuffer;
        private readonly float[] _rightBuffer;

        // Fade out state
        private bool _isFadingOut = false;
        private float _fadeOutDuration = 0f;
        private float _fadeOutTimer = 0f;
        private bool _isDisposed = false;
        public bool IsDisposed => _isDisposed;

        /// <summary>
        /// Creates a new MIDI streaming audio track.
        /// </summary>
        /// <param name="midiFile">The MIDI file to play</param>
        /// <param name="soundFont">The SoundFont to use for synthesis</param>
        /// <param name="sampleRate">Sample rate (default: 44100 Hz)</param>
        public MidiAudioTrack(MidiFile midiFile, SoundFontInstrument soundFontInstrument, int syncID, int sampleRate = MidiPlayer.DEFAULT_SAMPLE_RATE)
        {
            if (midiFile == null)
                throw new ArgumentNullException(nameof(midiFile));
            if (soundFontInstrument == null)
                throw new ArgumentNullException(nameof(soundFontInstrument));

            _sampleRate = sampleRate;

            // Create MIDI event processor with synthesizer
            _midiPlayer = new MidiPlayer(midiFile, soundFontInstrument, sampleRate);

            _synchronizer = new AudioTrackSynchronizer(syncID);

            // Initialize buffers
            int samplesPerChannel = bufferLength / 4; // 4 bytes per stereo sample
            _leftBuffer = new float[samplesPerChannel];
            _rightBuffer = new float[samplesPerChannel];

            // Calculate buffer duration for sample-accurate timing
            _bufferDurationSeconds = (double)samplesPerChannel / sampleRate;

            CreateSoundEffect(sampleRate, AudioChannels.Stereo);
        }

        /// <summary>
        /// Renders MIDI audio and submits it as a buffer.
        /// </summary>
        private void RenderAndSubmitBuffer()
        {
            double startTime = _currentTime;
            _currentTime += _bufferDurationSeconds;
            double endTime = _currentTime;

            _midiPlayer.RenderAudioWithEvents(
                _leftBuffer,
                _rightBuffer,
                startTime,
                endTime,
                _subBufferDivisor
            );

            ConvertToPCM16WithBoost(_leftBuffer, _rightBuffer, _bufferToSubmit);
            _soundEffectInstance.SubmitBuffer(_bufferToSubmit);
        }

        /// <summary>
        /// Renders MIDI audio with fade out applied and submits it as a buffer.
        /// </summary>
        private void RenderAndSubmitBufferWithVolume(float volume)
        {
            double startTime = _currentTime;
            _currentTime += _bufferDurationSeconds;
            double endTime = _currentTime;

            _midiPlayer.RenderAudio(
                _leftBuffer,
                _rightBuffer
                );

            if (volume != 1f)
            {
                // Apply fade out volume
                for (int i = 0; i < _leftBuffer.Length; i++)
                {
                    _leftBuffer[i] *= volume;
                    _rightBuffer[i] *= volume;
                }
            }

            ConvertToPCM16WithBoost(_leftBuffer, _rightBuffer, _bufferToSubmit);
            _soundEffectInstance.SubmitBuffer(_bufferToSubmit);
        }

        /// <summary>
        /// Fills remaining buffers with silence to match target count.
        /// </summary>
        private void FillBuffersWithSilence(int targetCount)
        {
            while (_soundEffectInstance.PendingBufferCount < targetCount)
            {
                ResetBuffer();
                _soundEffectInstance.SubmitBuffer(_bufferToSubmit);
            }
        }

        /// <summary>
        /// Converts float samples to PCM16 with volume boost.
        /// </summary>
        private void ConvertToPCM16WithBoost(float[] left, float[] right, byte[] output)
        {
            for (int i = 0; i < left.Length; i++)
            {
                // Apply volume boost
                float boostedLeft = left[i] * MidiPlayer.VOLUME_BOOST;
                float boostedRight = right[i] * MidiPlayer.VOLUME_BOOST;

                // Clamp and convert to PCM16
                short leftSample = (short)(Math.Clamp(boostedLeft, -1.0f, 1.0f) * short.MaxValue);
                short rightSample = (short)(Math.Clamp(boostedRight, -1.0f, 1.0f) * short.MaxValue);

                // Write to output buffer (interleaved stereo)
                int byteIndex = i * 4;
                output[byteIndex] = (byte)(leftSample & 0xFF);
                output[byteIndex + 1] = (byte)(leftSample >> 8 & 0xFF);
                output[byteIndex + 2] = (byte)(rightSample & 0xFF);
                output[byteIndex + 3] = (byte)(rightSample >> 8 & 0xFF);
            }
        }

        /// <summary>
        /// Seeks to a specific time in the MIDI file.
        /// Note: This resets the synthesizer state, so held notes will stop.
        /// </summary>
        /// <param name="timeInSeconds">Target time in seconds</param>
        public void SeekTo(double timeInSeconds)
        {

            if (timeInSeconds < 0.05)
            {
                timeInSeconds = 0;
            }

            // Clamp to valid range
            timeInSeconds = Math.Clamp(timeInSeconds, 0, TotalDuration);

            // Reset synthesizer and event processor
            _midiPlayer.Reset();

            // Fast-forward to target time in SILENT mode (skip NoteOn/NoteOff to avoid artifacts)
            // This preserves instrument state (Program Change, Control Change) without playing notes
            _midiPlayer.ProcessEventsUntil(timeInSeconds, silent: true);

            // Update current time
            _currentTime = timeInSeconds;
        }

        public override void PrepareToPlay()
        {
            base.PrepareToPlay();

            // Reset synthesizer and event processor
            _midiPlayer.Reset();

            // Seek to sync target position if available
            double initialTime = _synchronizer?.GetTargetPosition() ?? 0;
            initialTime = Math.Clamp(initialTime, 0, TotalDuration);

            if (initialTime > 0)
            {
                SeekTo(initialTime);
            }
            else
            {
                _currentTime = 0;
            }
        }

        public override void ReadAheadPutAChunkIntoTheBuffer()
        {
            if (_synchronizer == null || !_synchronizer.IsValid() || _isDisposed)
                return;

            if (_soundEffectInstance.State != SoundState.Playing &&
                _soundEffectInstance.State != SoundState.Paused)
                return;

            // Handle fade out
            if (_isFadingOut)
            {
                _fadeOutTimer += (float)_bufferDurationSeconds;

                // Calculate fade out progress (0 = start, 1 = end)
                float fadeProgress = Math.Min(_fadeOutTimer / _fadeOutDuration, 1f);

                if (fadeProgress >= 1f)
                {
                    Stop(AudioStopOptions.Immediate);
                    Main.QueueMainThreadAction(Dispose);
                    return;
                }

                // Render buffer with fade out applied
                RenderAndSubmitBufferWithVolume(1f - fadeProgress);
                return;
            }

            int currentBufferCount = _soundEffectInstance.PendingBufferCount;
            int targetBufferCount = _synchronizer.GetTargetBufferCount();

            var syncAction = _synchronizer.GetSyncAction(currentBufferCount);

            // Skip if too many buffers
            if (syncAction == BufferSyncAction.Skip)
                return;

            // Periodic re-synchronization to prevent drift
            double syncTime = _synchronizer.GetSynchronizedTime(_currentTime, TotalDuration);
            if (syncTime < _currentTime)
            {
                // Backwards - loop detected, need to seek
                SeekTo(syncTime);
            }
            else if (syncTime > _currentTime)
            {
                // Forward - normal sync, just update time
                _currentTime = syncTime;
            }
            // else: same time - no sync needed

            // Fill remaining buffers with silence if needed
            if (syncAction == BufferSyncAction.AddWithFill)
            {
                FillBuffersWithSilence(targetBufferCount - 1);
            }

            // Render and submit one audio buffer
            RenderAndSubmitBuffer();
        }

        public override void Reuse()
        {
            _midiPlayer.Reset();
            _currentTime = 0;
        }

        public override void Dispose()
        {
            if (_soundEffectInstance != null)
            {
                // Dispose SoundEffectInstance on main thread to avoid potential issues
                Main.QueueMainThreadAction(() =>
                {
                    try
                    {
                        _soundEffectInstance?.Dispose();
                    }
                    catch
                    {
                        Log.Warn("Failed to dispose SoundEffectInstance in MidiAudioTrack.");
                    }
                });
            }
            _isDisposed = true;
            _midiPlayer.Dispose();
        }

        /// <summary>
        /// Current playback time in seconds.
        /// </summary>
        public double CurrentTime => _currentTime;

        /// <summary>
        /// Gets the total duration of the MIDI file.
        /// </summary>
        public double TotalDuration => _midiPlayer?.TotalDuration ?? 0.0;

        /// <summary>
        /// Stops all playing notes and begins a fade out over the specified duration.
        /// After the fade completes, the track will be disposed automatically.
        /// </summary>
        /// <param name="fadeOutDuration">Duration of fade out in seconds (default: 2 seconds)</param>
        public void StopWithFadeOut(float fadeOutDuration = 2f)
        {
            // Stop all notes immediately (but gracefully with NoteOff)
            _midiPlayer.StopAllNotes();

            // Start fade out
            _isFadingOut = true;
            _fadeOutDuration = fadeOutDuration;
            _fadeOutTimer = 0f;
        }

        public bool isValid()
        {
            return _synchronizer.IsValid();
        }
    }
}

