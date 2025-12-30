using MeltySynth;
using System;

namespace Instrumentarria.Common.Systems.MidiEngine
{
    /// <summary>
    /// Manages MIDI playback and event processing.
    /// </summary>
    internal class MidiPlayer
    {
        public const int DEFAULT_SAMPLE_RATE = 44100;
        public const float VOLUME_BOOST = 7.0f;

        private readonly MidiFile _midiFile;
        private readonly Synthesizer _synthesizer;

        private int _currentEventIndex;
        
        // Instrument override control
        private bool _ignoreProgramChanges = false;
        
        // Track the last processed time for accurate sub-buffer processing
        private double _lastProcessedTime = 0.0;

        public double TotalDuration => _midiFile.Length.TotalSeconds;

        public MidiPlayer(MidiFile midiFile, SoundFontInstrument soundFontInstrument, int sampleRate = DEFAULT_SAMPLE_RATE)
        {
            if (midiFile == null)
                throw new ArgumentNullException(nameof(midiFile));
            if (soundFontInstrument == null)
                throw new ArgumentNullException(nameof(soundFontInstrument));

            _midiFile = midiFile;

            var settings = new SynthesizerSettings(sampleRate);
            settings.enableReverbAndChorus = true;
            _synthesizer = new Synthesizer(soundFontInstrument.SoundFont, settings);
            
            // Set initial instrument and enable override mode
            SetAllChannelsInstrument(soundFontInstrument.Bank, soundFontInstrument.Program);
            _ignoreProgramChanges = true; // Block MIDI file from changing instruments

            _currentEventIndex = -1;
        }

        /// <summary>
        /// Gets list of available instruments from a SoundFont.
        /// </summary>
        public static (int bank, int program, string name)[] GetInstruments(SoundFont soundFont)
        {
            if (soundFont == null)
                return Array.Empty<(int, int, string)>();

            var instruments = new System.Collections.Generic.List<(int, int, string)>();

            foreach (var preset in soundFont.Presets)
            {
                instruments.Add((preset.BankNumber, preset.PatchNumber, preset.Name));
            }

            return instruments.ToArray();
        }

        /// <summary>
        /// Processes all MIDI events up to the specified time.
        /// </summary>
        /// <param name="currentTime">Current playback time in seconds</param>
        /// <param name="silent">If true, only processes non-note events (used during seek to avoid sound artifacts)</param>
        public void ProcessEventsUntil(double currentTime, bool silent = false)
        {
            var targetTime = TimeSpan.FromSeconds(currentTime);

            // Process all events up to current time
            while (_currentEventIndex < _midiFile.Messages.Length && _midiFile.Times[_currentEventIndex] < targetTime)
            {
                var message = _midiFile.Messages[_currentEventIndex];

                // Only process Normal messages (skip TempoChange, LoopStart, LoopEnd, EndOfTrack)
                if (message.Type == MidiFile.MessageType.Normal)
                {
                    SendMessageToSynthesizer(message, silent);
                }
                _currentEventIndex++;
            }
        }

        /// <summary>
        /// Sends a MIDI message to the synthesizer.
        /// </summary>
        /// <param name="silent">If true, skips NoteOn/NoteOff events to avoid sound artifacts during seek</param>
        private void SendMessageToSynthesizer(MidiFile.Message message, bool silent = false)
        {
            switch ((MidiCommand)message.Command)
            {
                case MidiCommand.NoteOff:
                case MidiCommand.NoteOn:
                    // Skip note events during silent processing (seek)
                    if (!silent)
                    {
                        _synthesizer.ProcessMidiMessage(message.Channel, message.Command, message.Data1, message.Data2);
                    }
                    break;

                case MidiCommand.PolyphonicPressure:
                case MidiCommand.ControlChange:
                    // Block Bank Select messages if instrument override is enabled
                    if (_ignoreProgramChanges && (message.Data1 == 0 || message.Data1 == 32))
                    {
                        // Ignore Bank Select MSB (CC 0) and LSB (CC 32)
                        break;
                    }
                    _synthesizer.ProcessMidiMessage(message.Channel, message.Command, message.Data1, message.Data2);
                    break;

                case MidiCommand.ChannelPressure:
                case MidiCommand.PitchBend:
                    _synthesizer.ProcessMidiMessage(message.Channel, message.Command, message.Data1, message.Data2);
                    break;

                case MidiCommand.ProgramChange:
                    // Block Program Change if instrument override is enabled
                    if (_ignoreProgramChanges)
                    {
                        // Ignore instrument changes from MIDI file
                        break;
                    }
                    _synthesizer.ProcessMidiMessage(message.Channel, message.Command, message.Data1, 0);
                    break;
            }
        }

        /// <summary>
        /// Resets both the event processor and synthesizer state.
        /// This stops all playing notes and resets the event index to the beginning.
        /// </summary>
        public void Reset()
        {
            _synthesizer.Reset();
            _currentEventIndex = 0;
            _lastProcessedTime = 0.0;
        }

        /// <summary>
        /// Sets the instrument for a specific MIDI channel.
        /// </summary>
        /// <param name="channel">MIDI channel (0-15)</param>
        /// <param name="bank">Bank number (usually 0 for melodic instruments, 128 for percussion)</param>
        /// <param name="program">Program/Preset number (0-127)</param>
        public void SetChannelInstrument(int channel, int bank, int program)
        {
            if (channel < 0 || channel > 15)
                throw new ArgumentOutOfRangeException(nameof(channel), "MIDI channel must be between 0 and 15");
            if (bank < 0)
                throw new ArgumentOutOfRangeException(nameof(bank), "Bank number must be non-negative");
            if (program < 0 || program > 127)
                throw new ArgumentOutOfRangeException(nameof(program), "Program number must be between 0 and 127");

            // Send Bank Select MSB (Controller 0)
            _synthesizer.ProcessMidiMessage(channel, (int)MidiCommand.ControlChange, 0, (bank >> 7) & 0x7F);
            // Send Bank Select LSB (Controller 32)
            _synthesizer.ProcessMidiMessage(channel, (int)MidiCommand.ControlChange, 32, bank & 0x7F);
            // Send Program Change
            _synthesizer.ProcessMidiMessage(channel, (int)MidiCommand.ProgramChange, program, 0);
        }

        /// <summary>
        /// Sets the same instrument for all MIDI channels.
        /// </summary>
        /// <param name="bank">Bank number (usually 0 for melodic instruments, 128 for percussion)</param>
        /// <param name="program">Program/Preset number (0-127)</param>
        public void SetAllChannelsInstrument(int bank, int program)
        {
            for (int channel = 0; channel < 16; channel++)
            {
                SetChannelInstrument(channel, bank, program);
            }
        }

        /// <summary>
        /// Renders audio from the synthesizer with sub-buffer MIDI event processing.
        /// This allows for more accurate timing of MIDI events within the buffer.
        /// </summary>
        /// <param name="leftBuffer">Left channel output buffer</param>
        /// <param name="rightBuffer">Right channel output buffer</param>
        /// <param name="startTime">Start time of the buffer in seconds (where we left off last time)</param>
        /// <param name="endTime">End time of the buffer in seconds (where we need to get to)</param>
        /// <param name="subBufferDivisor">Divisor for sub-buffer size (e.g., 4 means buffer/4 per iteration)</param>
        public void RenderAudioWithEvents(float[] leftBuffer, float[] rightBuffer, double startTime, double endTime, int subBufferDivisor = 1)
        {
            // Handle backward seek (loop or manual seek)
            if (startTime > endTime)
            {
                // Reset and process from 0 to endTime
                Reset();
                startTime = 0.0;
            }
            
            int totalSamples = leftBuffer.Length;
            int subBufferSize = totalSamples / subBufferDivisor;
            
            // Ensure sub-buffer size is at least 1
            if (subBufferSize < 1)
                subBufferSize = 1;
            
            int processedSamples = 0;

            // Create temporary sub-buffers for rendering
            float[] subLeft = new float[subBufferSize];
            float[] subRight = new float[subBufferSize];

            // Calculate time progression per sample
            double timePerSample = (endTime - startTime) / totalSamples;

            while (processedSamples < totalSamples)
            {
                // Calculate how many samples to process in this iteration
                int samplesToProcess = Math.Min(subBufferSize, totalSamples - processedSamples);
                
                // Calculate the time point for this sub-buffer (from start to current position)
                double subBufferTime = startTime + (processedSamples * timePerSample);
                
                // Process MIDI events up to this time point
                ProcessEventsUntil(subBufferTime);

                // Render audio for this sub-buffer
                if (samplesToProcess == subBufferSize)
                {
                    // Full sub-buffer
                    _synthesizer.Render(subLeft, subRight);
                    Array.Copy(subLeft, 0, leftBuffer, processedSamples, samplesToProcess);
                    Array.Copy(subRight, 0, rightBuffer, processedSamples, samplesToProcess);
                }
                else
                {
                    // Partial sub-buffer (last chunk) - resize temp buffers
                    float[] partialLeft = new float[samplesToProcess];
                    float[] partialRight = new float[samplesToProcess];
                    _synthesizer.Render(partialLeft, partialRight);
                    Array.Copy(partialLeft, 0, leftBuffer, processedSamples, samplesToProcess);
                    Array.Copy(partialRight, 0, rightBuffer, processedSamples, samplesToProcess);
                }

                processedSamples += samplesToProcess;
            }
            
            // Update last processed time
            _lastProcessedTime = endTime;
        }

        /// <summary>
        /// Renders audio from the synthesizer (simple version without sub-buffer processing).
        /// </summary>
        /// <param name="leftBuffer">Left channel output buffer</param>
        /// <param name="rightBuffer">Right channel output buffer</param>
        public void RenderAudio(float[] leftBuffer, float[] rightBuffer)
        {
            _synthesizer.Render(leftBuffer, rightBuffer);
        }
    }
    /// <summary>
    /// MIDI command types (status byte upper nibble).
    /// </summary>
    internal enum MidiCommand : byte
    {
        NoteOff = 0x80,
        NoteOn = 0x90,
        PolyphonicPressure = 0xA0,
        ControlChange = 0xB0,
        ProgramChange = 0xC0,
        ChannelPressure = 0xD0,
        PitchBend = 0xE0
    }

    public class SoundFontInstrument
    {
        public int Bank { get; }
        public int Program { get; }
        public string Name { get; }
        public SoundFont SoundFont { get; set; }
        public SoundFontInstrument(SoundFont soundFont, int bank, int program, string name)
        {
            SoundFont = soundFont;
            Bank = bank;
            Program = program;
            Name = name;
        }
    }
}
