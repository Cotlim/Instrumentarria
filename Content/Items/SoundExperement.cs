using Instrumentarria.Common.Systems;
using Instrumentarria.MidiReader;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items
{
    // This example showcases how to loop and adjust sounds as they are playing. These are referred to as active sounds.
    // The weapon will shoot a projectile that will behave differently depending on how far away from the player the cursor is.
    // This allows the modder to experiment with each behavior independently to see how they work in game.
    public class SoundExperement : ModItem
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.TiedEighthNote}";

        public override void SetDefaults()
        {
            Item.useStyle = ItemUseStyleID.Swing;
            Item.width = 22;
            Item.height = 24;
            Item.maxStack = Item.CommonMaxStack;
            Item.UseSound = SoundID.Item1;
            Item.useAnimation = 15;
            Item.useTime = 15;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.value = Item.buyPrice(0, 0, 20, 0);
            Item.rare = ItemRarityID.Blue;
        }

        public override bool? UseItem(Player player)
        {
            // NOTE: MidiFile from MeltySynth doesn't expose Tracks publicly
            // Use MeltySynthMidiFileAdapter for event-based MIDI streaming instead
            /*
            foreach (var midiTrack in MidiAssets.TestMidi.Value.Tracks)
            {
                Log.Info($"[c/FFFF00:Track]: {midiTrack.Index}");
                foreach (var midiEvent in midiTrack.MidiEvents)
                {
                    Log.Info($"{midiEvent}");
                }
            }
            */

            ModContent.GetInstance<AudioSystem>().playSound = !ModContent.GetInstance<AudioSystem>().playSound;
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
