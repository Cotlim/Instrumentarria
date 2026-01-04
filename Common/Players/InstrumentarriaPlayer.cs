using Instrumentarria.Common.Systems.InstrumentsSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ModLoader;

namespace Instrumentarria.Common.Players
{
    public class InstrumentarriaPlayer : ModPlayer
    {
        public ITInstrument ActiveInstrument { get; private set; }

        public void ActivateInstrument(ITInstrument instrument)
        {
            ActiveInstrument = instrument;
        }

        public void DeactivateInstrument()
        {
            ActiveInstrument = null;
        }
    }
}
