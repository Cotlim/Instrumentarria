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
        private IAudioTrack _syncTarget;
        private AudioTrackSynchronizer _synchronizer;

        // Audio rendering buffers
        private readonly float[] _leftBuffer;
        private readonly float[] _rightBuffer;

        /// <summary>
        /// Creates a new MIDI streaming audio track.
        /// </summary>
        /// <param name="midiFile">The MIDI file to play</param>
        /// <param name="soundFont">The SoundFont to use for synthesis</param>
        /// <param name="sampleRate">Sample rate (default: 44100 Hz)</param>
        public MidiAudioTrack(MidiFile midiFile, SoundFontInstrument soundFontInstrument, int sampleRate = MidiPlayer.DEFAULT_SAMPLE_RATE)
        {
            if (midiFile == null)
                throw new ArgumentNullException(nameof(midiFile));
            if (soundFontInstrument == null)
                throw new ArgumentNullException(nameof(soundFontInstrument));

            _sampleRate = sampleRate;

            // Create MIDI event processor with synthesizer
            _midiPlayer = new MidiPlayer(midiFile, soundFontInstrument, sampleRate);

            // Initialize buffers
            int samplesPerChannel = bufferLength / 4; // 4 bytes per stereo sample
            _leftBuffer = new float[samplesPerChannel];
            _rightBuffer = new float[samplesPerChannel];

            // Calculate buffer duration for sample-accurate timing
            _bufferDurationSeconds = (double)samplesPerChannel / sampleRate;

            CreateSoundEffect(sampleRate, AudioChannels.Stereo);
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
            if (_synchronizer == null || !_synchronizer.IsValid())
                return;

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
            SubmitBufferIfPlaying();
        }

        /// <summary>
        /// Fills remaining buffers with silence to match target count.
        /// </summary>
        private void FillBuffersWithSilence(int targetCount)
        {
            while (_soundEffectInstance.PendingBufferCount < targetCount)
            {
                ResetBuffer();
                SubmitBufferIfPlaying();
            }
        }

        /// <summary>
        /// Submits the current buffer if the track is playing or paused.
        /// </summary>
        private void SubmitBufferIfPlaying()
        {
            if (_soundEffectInstance.State == SoundState.Playing || 
                _soundEffectInstance.State == SoundState.Paused)
            {
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
                Terraria.Main.QueueMainThreadAction(() =>
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

        /// <summary>
        /// Sets the audio track to synchronize playback with.
        /// Useful for synchronized playback with background music, loops, or dynamic time control.
        /// </summary>
        /// <param name="syncTarget">The audio track to synchronize with. Set to null to use internal timer.</param>
        public void SetSyncTarget(IAudioTrack syncTarget)
        {
            _syncTarget = syncTarget;
            _synchronizer = syncTarget != null ? new AudioTrackSynchronizer(syncTarget) : null;
        }

        /// <summary>
        /// Disables synchronization and returns to internal timing.
        /// </summary>
        public void UseInternalTiming()
        {
            _syncTarget = null;
            _synchronizer = null;
        }

        /// <summary>
        /// Sets the sub-buffer size for MIDI event processing.
        /// Smaller values provide better timing accuracy but increase CPU usage.
        /// </summary>
        /// <param name="size">Sub-buffer size in samples (recommended: 64-512). At 44100Hz: 256 samples = ~5.8ms precision</param>
        public void SetSubBufferSize(int size)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size), "Sub-buffer size must be positive");

            _subBufferDivisor = size;
        }

        /// <summary>
        /// Gets the current sub-buffer size used for MIDI event processing.
        /// </summary>
        public int SubBufferSize => _subBufferDivisor;
    }
}

