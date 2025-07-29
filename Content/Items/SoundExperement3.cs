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
    // This example showcases how to loop and adjust sounds as they are playing. These are referred to as active sounds.
    // The weapon will shoot a projectile that will behave differently depending on how far away from the player the cursor is.
    // This allows the modder to experiment with each behavior independently to see how they work in game.
    public class SoundExperement3 : ModItem
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
            return true;
        }

        private static float CalculateNormalizedDistance(Player player)
        {
            Vector2 playerToMouse = Main.MouseScreen + Main.screenPosition - player.Center;
            float distanceToScreenEdge = Math.Min(Main.screenHeight / 2, Main.screenWidth / 2) / Main.GameViewMatrix.Zoom.X;
            float normalizedDistance = playerToMouse.Length() / distanceToScreenEdge;
            return normalizedDistance;
        }
    }
}
