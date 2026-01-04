using Instrumentarria.Common.Players;
using Instrumentarria.Common.Systems.AssetsManagers;
using Instrumentarria.Common.Systems.InstrumentsSystem;
using Instrumentarria.Common.Systems.MidiEngine;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Instrumentarria.Content.Items.Instruments
{
    /// <summary>
    /// Test item that plays synchronized MIDI when held.
    /// Demonstrates background music -> MIDI synchronization system.
    /// </summary>
    public class SimpleSyncedInstrument : ModItem
    {
        public override string Texture => $"Terraria/Images/Item_{ItemID.MusicBox}";

        public ITInstrument instrument;

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.consumable = false;
            Item.maxStack = 1;
            Item.value = Item.buyPrice(0, 1, 0, 0);
            Item.rare = ItemRarityID.Blue;
            Item.color = Color.Blue;
            instrument = new ITInstrument(new(SoundFontAssets.DefaultSoundFont, 0, 0, "bells"), InstrumentType.Piano);
        }

        public override void UpdateInventory(Player player)
        {
            if (Main.myPlayer != player.whoAmI)
                return;

            // Deactivate when not holding
            bool isHolding = player.HeldItem?.type == Type;
            var midiTrackController = ModContent.GetInstance<MidiTracksController>();
            var instPlayer = player.GetModPlayer<InstrumentarriaPlayer>();
            if (!isHolding)
            {
                if (midiTrackController.IsActive(instPlayer))
                {
                    instPlayer.DeactivateInstrument();
                    midiTrackController.RemoveActivePlayer(instPlayer);
                    Main.NewText("MIDI Sync: Deactivated", Color.Gray);
                }
            }
            else
            {
                // Ensure active
                if (!midiTrackController.IsActive(instPlayer))
                {
                    instPlayer.ActivateInstrument(instrument);
                    midiTrackController.AddActivePlayer(instPlayer);
                    Main.NewText("MIDI Sync: Activated", Color.Cyan);
                }
            }
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.MusicBox)
                .AddIngredient(ItemID.Wood, 10)
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }
}
