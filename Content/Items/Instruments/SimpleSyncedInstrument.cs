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
        }

        public override void HoldItem(Player player)
        {
            if (Main.myPlayer != player.whoAmI)
                return;

            var midiSync = ModContent.GetInstance<BackgroundMidiSync>();
            var musicDetector = ModContent.GetInstance<MusicDetector>();

            // Activate MIDI sync when holding item
            if (!midiSync.IsActive)
            {
                midiSync.Activate();
                
                string musicName = musicDetector.GetMusicName(musicDetector.CurrentMusicSlot);
                
                if (midiSync.HasMappingFor(musicDetector.CurrentMusicSlot))
                {
                    Main.NewText($"MIDI Sync: {musicName}", Color.Cyan);
                }
                else
                {
                    Main.NewText($"No MIDI for: {musicName}", Color.Yellow);
                }
            }

            // Show UI
            ShowSyncStatus(player, midiSync, musicDetector);
        }

        private void ShowSyncStatus(Player player, BackgroundMidiSync midiSync, MusicDetector musicDetector)
        {
            string musicName = musicDetector.GetMusicName(musicDetector.CurrentMusicSlot);

            if (midiSync.IsPlaying)
            {
                player.cursorItemIconText =
                    $"Synced MIDI\n" +
                    $"Music: {musicName}\n" +
                    $"Time: {midiSync.CurrentTime:F1}s";
            }
            else if (midiSync.HasMappingFor(musicDetector.CurrentMusicSlot))
            {
                player.cursorItemIconText = $"Ready: {musicName}";
            }
            else
            {
                player.cursorItemIconText = $"No MIDI for:\n{musicName}";
            }

            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = Type;
        }

        public override void UpdateInventory(Player player)
        {
            if (Main.myPlayer != player.whoAmI)
                return;

            // Deactivate when not holding
            bool isHolding = player.HeldItem?.type == Type;

            if (!isHolding)
            {
                var midiSync = ModContent.GetInstance<BackgroundMidiSync>();
                
                if (midiSync.IsActive)
                {
                    midiSync.Deactivate();
                    Main.NewText("MIDI Sync: Deactivated", Color.Gray);
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
