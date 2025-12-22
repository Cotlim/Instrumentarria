using MeltySynth;
using Microsoft.Xna.Framework.Audio;
using System;
using Terraria.Audio;

namespace Instrumentarria.Common.Systems.RhythmEngine
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

        // Playback control - allows external time control
        private Func<double> _externalTimeProvider;
        private bool UseExternalTime => _externalTimeProvider != null;

        // Audio rendering buffers
        private readonly float[] _leftBuffer;
        private readonly float[] _rightBuffer;

        /// <summary>
        /// Creates a new MIDI streaming audio track.
        /// </summary>
        /// <param name="midiFile">The MIDI file to play</param>
        /// <param name="soundFont">The SoundFont to use for synthesis</param>
        /// <param name="sampleRate">Sample rate (default: 44100 Hz)</param>
        public MidiAudioTrack(MidiFile midiFile, SoundFont soundFont, int sampleRate = MidiPlayer.DEFAULT_SAMPLE_RATE)
        {
            if (midiFile == null)
                throw new ArgumentNullException(nameof(midiFile));
            if (soundFont == null)
                throw new ArgumentNullException(nameof(soundFont));

            // Create MIDI event processor with synthesizer
            _midiPlayer = new MidiPlayer(midiFile, soundFont, sampleRate);

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

            // If using external time provider, seek to initial position
            if (UseExternalTime)
            {
                double initialTime = _externalTimeProvider();
                initialTime = Math.Clamp(initialTime, 0, TotalDuration);

                // Always use SeekTo to properly initialize state in silent mode
                if (initialTime > 0)
                {
                    SeekTo(initialTime);
                }
                else
                {
                    _currentTime = 0;
                }
            }
            else
            {
                // Normal playback from start
                _currentTime = 0;
            }
        }

        public override void ReadAheadPutAChunkIntoTheBuffer()
        {

            // Update time based on mode
            UpdatePlaybackTime();

            // Process MIDI events up to current time
            // Use silent mode if we just seeked to avoid sound artifacts
            _midiPlayer.ProcessEventsUntil(_currentTime);

            // Render audio from synthesizer
            _midiPlayer.RenderAudio(_leftBuffer, _rightBuffer);

            // Convert to PCM16 with volume boost
            ConvertToPCM16WithBoost(_leftBuffer, _rightBuffer, _bufferToSubmit);

            // Check if playback should stop
            bool shouldStop = ShouldStopPlayback();

            // Submit buffer only if not stopping and playing/paused
            if (shouldStop || _soundEffectInstance.State != SoundState.Playing && _soundEffectInstance.State != SoundState.Paused)
                return;

            _soundEffectInstance.SubmitBuffer(_bufferToSubmit);
        }

        /// <summary>
        /// Updates playback time based on internal timer or external provider.
        /// </summary>
        private void UpdatePlaybackTime()
        {
            if (UseExternalTime)
            {
                // Use external time provider (allows for synced playback, loops, etc.)
                double newTime = _externalTimeProvider();

                newTime = Math.Clamp(newTime, 0, TotalDuration);

                // Check if we need to seek backwards
                // MidiEventProcessor tracks current event index, so going backwards requires reset
                if (newTime < _currentTime)
                {
                    SeekTo(newTime);
                }
                else
                {
                    _currentTime = newTime;
                }
                return;
            }

            // Use internal timer (normal playback)
            _currentTime += _bufferDurationSeconds;
        }

        /// <summary>
        /// Determines if playback should stop based on event state and timing.
        /// </summary>
        private bool ShouldStopPlayback()
        {
            // Allow time for note release/decay (1 second after last event)
            if (_currentTime > TotalDuration + 1.0)
            {
                return true;
            }

            return false;
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

            if (timeInSeconds < 0.02)
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
        /// Sets an external time provider function that controls playback position.
        /// Useful for synchronized playback, loops, or dynamic time control.
        /// </summary>
        /// <param name="timeProvider">Function that returns current playback time in seconds. Set to null to use internal timer.</param>
        public void SetExternalTimeProvider(Func<double> timeProvider)
        {
            _externalTimeProvider = timeProvider;
        }

        /// <summary>
        /// Disables external time provider and returns to internal timing.
        /// </summary>
        public void UseInternalTiming()
        {
            _externalTimeProvider = null;
        }
    }
}

