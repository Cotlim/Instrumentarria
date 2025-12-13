namespace Instrumentarria.MidiReader
{
    using System;
    using System.Collections.Generic;

    public class MidiFile
    {
        public readonly int Format;
        public readonly int TicksPerQuarterNote;
        public readonly MidiTrack[] Tracks;

        public MidiFile(int format, int ticksPerQuarterNote, MidiTrack[] tracks)
        {
            Format = format;
            TicksPerQuarterNote = ticksPerQuarterNote;
            Tracks = tracks;
        }
    }

    public class MidiTrack
    {
        public int Index;
        public List<MidiEvent> MidiEvents = new List<MidiEvent>();
        public List<TextEvent> TextEvents = new List<TextEvent>();
    }

    // ---- Base classes ----
    public abstract class MidiEvent
    {
        public int Time { get; set; }
        public byte Arg1 { get; set; } // first data byte
        public byte Arg2 { get; set; } // second data byte (or 0)
        public byte Arg3 { get; set; } // channel for channel-events, or extra meta byte

        // Зручно: у channel-подій канал зберігається в Arg3
        public byte Channel => Arg3;
    }

    public abstract class MetaEvent : MidiEvent
    {
    }

    // ---- Channel events ----
    public class NoteOnEvent : MidiEvent
    {
        public byte Note => Arg1;
        public byte Velocity => Arg2;

        public override string ToString()
        {
            return $"Time: {Time} Note: {Note}, Velocity: {Velocity}";
        }
    }

    public class NoteOffEvent : MidiEvent
    {
        public byte Note => Arg1;
        public byte Velocity => Arg2;

        public override string ToString()
        {
            return $"EndTime: {Time} Note: {Note}, Velocity: {Velocity}";
        }
    }

    public class ControlChangeEvent : MidiEvent
    {
        public byte Controller => Arg1;
        public byte Value => Arg2;
        // Channel available via Channel property (Arg3)

        override public string ToString()
        {
            return $"Time: {Time} Controller: {Controller}, Value: {Value}, Channel: {Channel}";
        }
    }

    public class ProgramChangeEvent : MidiEvent
    {
        public byte Program => Arg1;

        public override string ToString()
        {
            return $"Time: {Time} Program: {Program}";
        }
    }

    public class ChannelAfterTouchEvent : MidiEvent
    {
        public byte Pressure => Arg1;

        public override string ToString()
        {
            return $"Time: {Time} Pressure: {Pressure}, Channel: {Channel}";
        }
    }

    public class PitchBendEvent : MidiEvent
    {
        // 14-bit value: LSB = Arg1, MSB = Arg2
        public int Value => (Arg2 << 7) | Arg1;

        public override string ToString()
        {
            return $"Time: {Time} Pitch Bend Value: {Value}, Channel: {Channel}";
        }
    }

    // ---- Meta events ----
    public class TempoChangeEvent : MetaEvent
    {
        // Mpqn stored as Arg1..Arg3
        public int Mpqn => (Arg1 << 16) | (Arg2 << 8) | Arg3;
        public double Bpm => Mpqn == 0 ? 0.0 : 60000000.0 / Mpqn;

        public override string ToString()
        {
            return $"Time: {Time} Tempo: {Bpm:F2} BPM";
        }
    }

    public class TimeSignatureEvent : MetaEvent
    {
        // We stored nn, ddPow, cc in Arg1..Arg3 (bb omitted here)
        public byte Numerator => Arg1;
        public byte DenominatorPower => Arg2; // real denominator = 2^DenominatorPower
        public int Denominator => 1 << DenominatorPower;
        public byte MidiClocksPerClick => Arg3;
        // If you need 'bb' (32nd notes count), add a field or change storage.

        public override string ToString()
        {
            return $"Time: {Time} Time Signature: {Numerator}/{Denominator}, MidiClocksPerClick: {MidiClocksPerClick}";
        }
    }

    public class KeySignatureEvent : MetaEvent
    {
        // sf is signed (-7..+7): flats if negative, sharps if positive
        public sbyte Sf => (sbyte)Arg1;
        public byte Mi => Arg2; // 0 = major, 1 = minor

        public override string ToString()
        {
            string[] majorKeys = { "Cb", "Gb", "Db", "Ab", "Eb", "Bb", "F", "C", "G", "D", "A", "E", "B", "F#", "C#" };
            string[] minorKeys = { "Abm", "Ebm", "Bbm", "Fm", "Cm", "Gm", "Dm", "Am", "Em", "Bm", "F#m", "C#m", "G#m", "D#m", "A#m" };
            string key = (Mi == 0) ? majorKeys[Sf + 7] : minorKeys[Sf + 7];
            return $"Time: {Time} Key Signature: {key}";
        }
    }

    public class TrackNameEvent : MetaEvent
    {
        // Parser currently stores track/text events in TextEvent list.
        // This class left for completeness if you want meta-text as MidiEvent.
    }

    public class EndOfTrackEvent : MetaEvent
    {
        public override string ToString()
        {
            return $"Time: {Time} End of Track";
        }
    }

    public class MidiEventGeneric : MidiEvent { }
    public class MetaEventGeneric : MetaEvent { }

    // ---- Text event container (parser uses this) ----
    public struct TextEvent
    {
        public int Time;
        public byte Type;
        public string Value;
        public TextEventType TextEventType => (TextEventType)Type;
    }

    // ---- Factory ----
    public static class MidiEventFactory
    {
        // Create channel / voice events
        // type: high nibble e.g. 0x90 for NoteOn, arg1=data1, arg2=data2, arg3=channel (0..15)
        public static MidiEvent CreateMidiEvent(int time, byte type, byte arg1, byte arg2, byte arg3)
        {
            var evType = (MidiEventType)type;
            switch (evType)
            {
                case MidiEventType.NoteOn:
                    return new NoteOnEvent { Time = time, Arg1 = arg1, Arg2 = arg2, Arg3 = arg3 };
                case MidiEventType.NoteOff:
                    return new NoteOffEvent { Time = time, Arg1 = arg1, Arg2 = arg2, Arg3 = arg3 };
                case MidiEventType.ControlChange:
                    return new ControlChangeEvent { Time = time, Arg1 = arg1, Arg2 = arg2, Arg3 = arg3 };
                case MidiEventType.ProgramChange:
                    return new ProgramChangeEvent { Time = time, Arg1 = arg1, Arg2 = arg2, Arg3 = arg3 };
                case MidiEventType.ChannelAfterTouch:
                    return new ChannelAfterTouchEvent { Time = time, Arg1 = arg1, Arg2 = arg2, Arg3 = arg3 };
                case MidiEventType.PitchBendChange:
                    return new PitchBendEvent { Time = time, Arg1 = arg1, Arg2 = arg2, Arg3 = arg3 };
                default:
                    return new MidiEventGeneric { Time = time, Arg1 = arg1, Arg2 = arg2, Arg3 = arg3 };
            }
        }

        // Create meta events
        // metaType is the meta event type byte (e.g. 0x51), arg1..arg3 are already parsed bytes (or 0)
        public static MetaEvent CreateMetaEvent(int time, byte metaType, byte arg1, byte arg2, byte arg3)
        {
            switch ((MetaEventType)metaType)
            {
                case MetaEventType.Tempo:
                    return new TempoChangeEvent { Time = time, Arg1 = arg1, Arg2 = arg2, Arg3 = arg3 };
                case MetaEventType.TimeSignature:
                    return new TimeSignatureEvent { Time = time, Arg1 = arg1, Arg2 = arg2, Arg3 = arg3 };
                case MetaEventType.KeySignature:
                    return new KeySignatureEvent { Time = time, Arg1 = arg1, Arg2 = arg2, Arg3 = arg3 };
                // If you want to convert track-name meta into MidiEvent (instead of TextEvent),
                // you can handle 0x03 here and put the string into a different structure.
                case (MetaEventType)0x2F: // End of Track
                    return new EndOfTrackEvent { Time = time, Arg1 = arg1, Arg2 = arg2, Arg3 = arg3 };
                default:
                    return new MetaEventGeneric { Time = time, Arg1 = arg1, Arg2 = arg2, Arg3 = arg3 };
            }
        }
    }

    // ---- Enums ----
    public enum MidiEventType : byte
    {
        NoteOff = 0x80,
        NoteOn = 0x90,
        KeyAfterTouch = 0xA0,
        ControlChange = 0xB0,
        ProgramChange = 0xC0,
        ChannelAfterTouch = 0xD0,
        PitchBendChange = 0xE0,
        MetaEvent = 0xFF
    }

    public enum ControlChangeType : byte
    {
        BankSelect = 0x00,
        Modulation = 0x01,
        Volume = 0x07,
        Balance = 0x08,
        Pan = 0x0A,
        Sustain = 0x40
    }

    public enum TextEventType : byte
    {
        Text = 0x01,
        TrackName = 0x03,
        Lyric = 0x05,
    }

    public enum MetaEventType : byte
    {
        EndOfTrack = 0x2F,
        Tempo = 0x51,
        TimeSignature = 0x58,
        KeySignature = 0x59
    }
}
