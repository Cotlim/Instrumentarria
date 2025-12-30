using Instrumentarria.Common.Systems;
using Instrumentarria.CustomSoundBankReader;
using Microsoft.Xna.Framework.Audio;
using System;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities.Terraria.Utilities;
using XPT.Core.Audio.MP3Sharp;

namespace ExampleMod.Content.Items
{
    // only for testing purpose
    public class CustomCuesSwitcher : ModItem
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.TiedEighthNote}";

        public override void SetDefaults()
        {
            Item.useStyle = ItemUseStyleID.Swing;
            Item.width = 22;
            Item.height = 24;
            Item.maxStack = Item.CommonMaxStack;
            Item.useAnimation = 15;
            Item.useTime = 15;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.value = Item.buyPrice(0, 0, 20, 0);
            Item.rare = ItemRarityID.Blue;
            Item.autoReuse = true;
        }

        public override bool? UseItem(Player player)
        {
            ModContent.GetInstance<ReplaceAllCuesSystem>().Toggle();
            if (ModContent.GetInstance<ReplaceAllCuesSystem>().IsTurnedOn)
            {
                Main.NewText("New Cues Enabled!", Color.Yellow);
            }
            else
            {
                Main.NewText("Old Cues Enabled!", Color.Yellow);
            }
                return true;
        }
    }
}
