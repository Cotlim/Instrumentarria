using Instrumentarria.Common.Systems.RhythmEngine;
using Microsoft.Xna.Framework.Audio;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Instrumentarria.Content.Items.Instruments
{
    /// <summary>
    /// Простий предмет для тестування окремих MIDI нот
    /// </summary>
    public class SingleNoteTester : ModItem
    {
        private static byte currentNote = 60; // Middle C

        public override string Texture => $"Terraria/Images/Item_{ItemID.Bell}";

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 10;
            Item.useAnimation = 10;
            Item.autoReuse = true;
            Item.consumable = false;
            Item.maxStack = 1;
            Item.value = Item.buyPrice(0, 0, 50, 0);
            Item.rare = ItemRarityID.White;
        }

        public override bool? UseItem(Player player)
        {
            if (Main.myPlayer == player.whoAmI)
            {
                // Використовуємо StreamingMidiPlayer (об'єднаний клас)
                var player_synth = ModContent.GetInstance<MidiPlayer>();
                if (player_synth.IsReady)
                {
                    player_synth.PlayNote(currentNote, 127, 0.8f, 1.5f);
                    Main.NewText($"StreamingMidiPlayer: {GetNoteName(currentNote)} ({currentNote})", 100, 200, 255);
                }
                else
                {
                    Main.NewText("StreamingMidiPlayer not ready! Check logs for details.", 255, 100, 100);
                }
            }

            return true;
        }

        public override bool AltFunctionUse(Player player)
        {
            return true;
        }

        public override bool CanUseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                // Right-click: змінити ноту
                currentNote++;
                if (currentNote > 84) // До C6
                    currentNote = 48; // Від C3

                Main.NewText($"Changed to: {GetNoteName(currentNote)} ({currentNote})", 150, 255, 100);
                return false; // Не виконуй UseItem
            }

            return true;
        }

        public override void HoldItem(Player player)
        {
            if (Main.myPlayer == player.whoAmI)
            {
                player.cursorItemIconText = $"{GetNoteName(currentNote)} [Streaming]\n" +
                                           $"L-Click: Play | R-Click: Change Note";
                player.cursorItemIconEnabled = true;
                player.cursorItemIconID = Type;
            }
        }

        /// <summary>
        /// Конвертує MIDI номер в назву ноти
        /// </summary>
        private static string GetNoteName(byte midiNote)
        {
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int octave = (midiNote / 12) - 1;
            int noteIndex = midiNote % 12;
            return $"{noteNames[noteIndex]}{octave}";
        }
    }
}
