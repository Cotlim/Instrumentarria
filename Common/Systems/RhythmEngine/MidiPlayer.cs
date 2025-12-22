using MeltySynth;
using System;

namespace Instrumentarria.Common.Systems.RhythmEngine
{
    /// <summary>
    /// Manages MIDI playback and event processing.
    /// </summary>
    internal class MidiPlayer
    {
        public const int DEFAULT_SAMPLE_RATE = 44100;
        public const float VOLUME_BOOST = 8.0f;
        
        private readonly MidiFile _midiFile;
        private readonly Synthesizer _synthesizer;
        
        private int _currentEventIndex;

        public double TotalDuration => _midiFile.Length.TotalSeconds;

        public MidiPlayer(MidiFile midiFile, SoundFont soundFont, int sampleRate = DEFAULT_SAMPLE_RATE)
        {
            if (midiFile == null)
                throw new ArgumentNullException(nameof(midiFile));
            if (soundFont == null)
                throw new ArgumentNullException(nameof(soundFont));

            _midiFile = midiFile;

            var settings = new SynthesizerSettings(sampleRate);
            _synthesizer = new Synthesizer(soundFont, settings);
            
            _currentEventIndex = 0;
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
                case MidiCommand.ChannelPressure:
                case MidiCommand.PitchBend:
                    _synthesizer.ProcessMidiMessage(message.Channel, message.Command, message.Data1, message.Data2);
                    break;
                    
                case MidiCommand.ProgramChange:
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
        }
        
        /// <summary>
        /// Renders audio from the synthesizer.
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
}
