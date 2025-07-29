using Instrumentarria.CustomSoundBankReader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria.ModLoader.Engine;

namespace Instrumentarria.Common.Systems
{
    internal class ReplaceAllCuesSystem : ModSystem
    {
        public WaveBankReader waveBankReader;

        private Dictionary<int, IAudioTrack> oldCues;
        private Dictionary<int, IAudioTrack> newCues;

        public bool IsTurnedOn = true;

        public override void OnModLoad()
        {
            var contentManager = (TMLContentManager)Main.instance.Content;
            waveBankReader = new WaveBankReader(contentManager.GetPath("Wave Bank.xwb"));
            oldCues = new Dictionary<int, IAudioTrack>();
            newCues = new Dictionary<int, IAudioTrack>();
            ushort waveIndex = 0;
            if (Main.audioSystem is LegacyAudioSystem legacyAudioSystem)
            {
                for (int i = 0; i < legacyAudioSystem.AudioTracks.Length; i++)
                {
                    if (legacyAudioSystem.AudioTracks[i] is CueAudioTrack cueAudioTrack)
                    {
                        waveIndex = FAudio.FACTSoundBank_GetCueIndex(cueAudioTrack._soundBank.handle, cueAudioTrack._cueName);
                        var track = new WaveBankAudioTrack(waveBankReader, waveIndex, cueAudioTrack._cueName);
                        oldCues.Add(i, legacyAudioSystem.AudioTracks[i]);
                        newCues.Add(i, track);
                        cueAudioTrack.Stop(Microsoft.Xna.Framework.Audio.AudioStopOptions.Immediate);
                        legacyAudioSystem.AudioTracks[i] = track;
                    }
                }
            }
        }

        public override void OnModUnload()
        {

            if (Main.audioSystem is LegacyAudioSystem legacyAudioSystem)
            {
                foreach (KeyValuePair<int, IAudioTrack> pair in oldCues)
                {
                    legacyAudioSystem.AudioTracks[pair.Key] = pair.Value;
                }
            }
        }

        public void Toggle()
        {
            IsTurnedOn = !IsTurnedOn;
            if (IsTurnedOn) {
                if (Main.audioSystem is LegacyAudioSystem legacyAudioSystem)
                {
                    foreach (KeyValuePair<int, IAudioTrack> pair in newCues)
                    {
                        legacyAudioSystem.AudioTracks[pair.Key].Stop(Microsoft.Xna.Framework.Audio.AudioStopOptions.Immediate);
                        legacyAudioSystem.AudioTracks[pair.Key] = pair.Value;
                        
                    }
                }
            }
            else
            {
                if (Main.audioSystem is LegacyAudioSystem legacyAudioSystem)
                {
                    foreach (KeyValuePair<int, IAudioTrack> pair in oldCues)
                    {
                        legacyAudioSystem.AudioTracks[pair.Key].Stop(Microsoft.Xna.Framework.Audio.AudioStopOptions.Immediate);
                        legacyAudioSystem.AudioTracks[pair.Key] = pair.Value;
                        
                    }
                }
            }
            
        }

    }
}
