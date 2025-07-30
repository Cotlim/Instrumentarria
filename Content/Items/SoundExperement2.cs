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
    public class SoundExperement2 : ModItem
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.QuarterNote}";

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
            var audioSystem = ModContent.GetInstance<AudioSystem>();
            float pich = CalculateNormalizedDistance(player);
            float frecRatio = (float)Math.Pow(2.0, pich);
            long targetPosition = 0;
            if (Main.audioSystem is LegacyAudioSystem legacyAudioSystem)
            {
                if (legacyAudioSystem.AudioTracks[Main.curMusic] is ASoundEffectBasedAudioTrack audioTrack)
                {
                    if (audioTrack is OGGAudioTrack oggTrack)
                    {
                        // Перемотати (SeekTo вже автоматично обробляє wrap-around)
                        float mouseYrelative = Main.MouseScreen.Y / Main.ScreenSize.Y * Main.GameViewMatrix.Zoom.X;

                        targetPosition = (long)(mouseYrelative * oggTrack._vorbisReader.TotalSamples);
                        //oggTrack._vorbisReader.SeekTo(targetPosition % oggTrack._vorbisReader.TotalSamples);
                        oggTrack._soundEffectInstance.Pitch = -mouseYrelative*4 + 2;
                        
                        return false;
                    }
                    else if (audioTrack is MP3AudioTrack mp3Track)
                    {
                        targetPosition = (long)(pich * mp3Track._mp3Stream.Length);
                        mp3Track._mp3Stream.Seek(targetPosition % mp3Track._mp3Stream.Length, System.IO.SeekOrigin.Begin);
                    }
                    else if (audioTrack is WaveBankAudioTrack waveBankAudioTrack)
                    {
                        float mouseYrelative = Main.MouseScreen.Y / Main.ScreenSize.Y * Main.GameViewMatrix.Zoom.X;

                        //waveBankAudioTrack._soundEffectInstance.Pitch = -mouseYrelative * 4 + 2;
                        //Main.NewText($"{mouseYrelative}");
                        targetPosition = (long)(mouseYrelative * waveBankAudioTrack._stream.Length);
                        waveBankAudioTrack._stream.Seek(targetPosition, System.IO.SeekOrigin.Begin);

                        return false;
                    }
                }
                else if (legacyAudioSystem.AudioTracks[Main.curMusic] is CueAudioTrack cueAudioTrack)
                {
                    string cueName = cueAudioTrack._cue.Name;

                    Main.NewText("Name: " + cueName);

                }
            }
            return true;
        }

        private void Crossfade(float[] tail, float[] head, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                head[i] = tail[i] * (1.0f - t) + head[i] * t;
            }
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
