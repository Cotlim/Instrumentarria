using Instrumentarria.Common.Systems.MidiEngine;

namespace Instrumentarria.Common.Systems.InstrumentsSystem
{
    public class ITInstrument
    {
        public SoundFontInstrument SoundFontAsset { get; private set; }
        public InstrumentType Type { get; private set; }

        public ITInstrument(SoundFontInstrument soundFontAsset, InstrumentType type)
        {
            SoundFontAsset = soundFontAsset;
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
}
