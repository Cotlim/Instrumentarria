using Microsoft.Xna.Framework.Audio;
using MonoMod.RuntimeDetour;
using ReLogic.Utilities;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace Instrumentarria.Common.Systems
{
    internal class AudioSystem : ModSystem
    {
        public bool playSound = false;
        public SlotId soundSlot;
        public Hook SoundEffectInstanceSetPitchHook;
        public Hook SoundEffectInstanceUpdatePitchHook;
        public float pitch = 0.5f;
        public bool isHigherPichDetected = false;

        //SoundStyle soundStyleIgniteLoop = new SoundStyle("Instrumentarria/Assets/sin")
        SoundStyle soundStyleIgniteLoop = new SoundStyle("Terraria/Sounds/Roar_0")
        {
            IsLooped = true,
            SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
        };

        public override void OnModLoad()
        {
            Assembly assembly = Assembly.GetAssembly(typeof(SoundEffectInstance));
            PropertyInfo propertyInfo = typeof(SoundEffectInstance).GetProperty(nameof(SoundEffectInstance.Pitch));
            MethodInfo methodInfo = propertyInfo.GetSetMethod(true);
            SoundEffectInstanceSetPitchHook = new(methodInfo, (Action<SoundEffectInstance, float> orig, SoundEffectInstance self, float value) =>
            {
                //orig(self, value);
                self.INTERNAL_pitch = value;
                if (self.handle != IntPtr.Zero)
                {
                    self.UpdatePitch();
                }
            });
        }

        public override void OnModUnload()
        {
            SoundEffectInstanceSetPitchHook?.Dispose();
        }

        public override void PostUpdateEverything()
        {
            if (!playSound)
            {
                return;
            }

            if (!SoundEngine.TryGetActiveSound(soundSlot, out var _))
            {
                soundSlot = SoundEngine.PlaySound(soundStyleIgniteLoop, null, AdvancedSoundUpdateCallback);
            }
        }

        private bool AdvancedSoundUpdateCallback(ActiveSound soundInstance)
        {
            soundInstance.Pitch = pitch;

            return playSound;
        }
    }

}
