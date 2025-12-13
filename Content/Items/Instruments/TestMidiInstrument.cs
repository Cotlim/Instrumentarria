using Instrumentarria.Common.Systems.RhythmEngine;
using Instrumentarria.MidiReader;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Instrumentarria.Content.Items.Instruments
{
    /// <summary>
    /// Тестовий інструмент для програвання MIDI з SoundFont
    /// </summary>
    public class TestMidiInstrument : ModItem
    {
        public override string Texture => $"Terraria/Images/Item_{ItemID.MusicBox}";

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.autoReuse = false;
            Item.consumable = false;
            Item.maxStack = 1;
            Item.value = Item.buyPrice(0, 1, 0, 0);
            Item.rare = ItemRarityID.Blue;
            Item.UseSound = SoundID.Item1;
        }

        public override bool? UseItem(Player player)
        {
            if (Main.myPlayer == player.whoAmI)
            {
                // Використовуємо StreamingMidiPlayer (об'єднаний клас)
                var midiPlayer = ModContent.GetInstance<MidiPlayer>();

                // Якщо вже грає - зупинити
                if (midiPlayer.IsPlaying)
                {
                    midiPlayer.Stop();
                    Main.NewText("MIDI Playback Stopped", 255, 100, 100);
                    return true;
                }

                // Завантажити Test.mid
                if (MidiAssets.TestMidi?.IsLoaded == true)
                {
                    var midiFile = MidiAssets.TestMidi.Value;

                    // Завантажити в streaming player
                    midiPlayer.LoadMidi(midiFile);

                    // Почати відтворення
                    midiPlayer.Play();

                    Main.NewText("Playing MIDI (Streaming): Test.mid", 100, 255, 100);
                    Main.NewText(midiPlayer.GetInfo(), 150, 150, 255);
                }
                else
                {
                    Main.NewText("MIDI file not loaded!", 255, 50, 50);
                }
            }

            return true;
        }

        public override void HoldItem(Player player)
        {
            // Показати інформацію при утриманні
            if (Main.myPlayer == player.whoAmI && player.itemTime == 0)
            {
                var midiPlayer = ModContent.GetInstance<MidiPlayer>();
                
                if (midiPlayer.IsPlaying)
                {
                    player.cursorItemIconText = $"Streaming: {midiPlayer.CurrentTime:F1}s / {midiPlayer.BPM:F0} BPM";
                }
                else
                {
                    player.cursorItemIconText = "Click to play MIDI (Streaming Mode)";
                }
                
                player.cursorItemIconEnabled = true;
                player.cursorItemIconID = Type;
            }
        }

        public override void SetStaticDefaults()
        {
            // Tooltip
            // Можна додати локалізацію пізніше
        }
    }
}
