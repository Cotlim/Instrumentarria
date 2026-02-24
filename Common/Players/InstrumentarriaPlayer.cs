using Instrumentarria.Common.Systems.InstrumentsSystem;
using Terraria.ModLoader;

namespace Instrumentarria.Common.Players
{
    public class InstrumentarriaPlayer : ModPlayer
    {
        public ITInstrument ActiveInstrument { get; private set; }

        public bool IsActive => ActiveInstrument != null;

        public void ActivateInstrument(ITInstrument instrument)
        {
            ActiveInstrument = instrument;
        }

        public void DeactivateInstrument()
        {
            ActiveInstrument = null;
        }

        public override void SendClientChanges(ModPlayer clientPlayer)
        {
            base.SendClientChanges(clientPlayer);
        }

        public override void CopyClientState(ModPlayer targetCopy)
        {
            base.CopyClientState(targetCopy);
        }
    }
}
