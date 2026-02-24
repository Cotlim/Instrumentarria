using Instrumentarria.Common.Systems.MidiEngine;
using MeltySynth;

namespace Instrumentarria.Common.Systems.InstrumentsSystem
{
    public class ITInstrument
    {
        public SoundFontInstrument SFInstrument { get; private set; }
        public InstrumentType Type { get; private set; }

        public ITInstrument(SoundFontInstrument soundFontInstrument, InstrumentType type)
        {
            SFInstrument = soundFontInstrument;
            Type = type;
        }
    }

    public enum InstrumentType
    {
        Vocal,
        Piano,
        Bass,
        Drums,
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
