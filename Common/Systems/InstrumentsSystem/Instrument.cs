using MeltySynth;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Instrumentarria.Common.Systems.InstrumentsSystem
{
    internal class Instrument
    {
        private Asset<SoundFont> _soundFontAsset;
        private InstrumentType _type;
    }

    public enum InstrumentType
    {
        Vocal,
        Piano,
        Bass,
        Drums,
    }
}
