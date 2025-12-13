using Instrumentarria.MidiReader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ModLoader;

namespace Instrumentarria
{
    // Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
    public class Instrumentarria : Mod
    {
        public override void Close()
        {
            ModContent.GetInstance<MidiAssets>().CloseStreams();
            base.Close();
        }
    }
}
