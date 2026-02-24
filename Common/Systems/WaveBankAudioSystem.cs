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
    /// <summary>
    /// System responsible for replacing vanilla audio tracks with custom WaveBank-based tracks.
    /// Manages the lifecycle of audio track replacements and provides position tracking.
    /// </summary>
    internal class WaveBankAudioSystem : ModSystem
    {
        public WaveBankReader waveBankReader;
        public SoundBankReader soundBankReader;

        private Dictionary<int, IAudioTrack> oldCues;
        private Dictionary<int, IAudioTrack> newCues;
        private Dictionary<IAudioTrack, ushort> trackToWaveIndex;

        public bool IsTurnedOn = true;

        public override void OnModLoad()
        {
            if (Main.audioSystem is not LegacyAudioSystem legacyAudioSystem)
            {
                Log.Warn("Audio system is not supported!");
                return;
            }

            // Load the wave bank and sound bank using the custom readers
            var contentManager = (TMLContentManager)Main.instance.Content;
            waveBankReader = new WaveBankReader(contentManager.GetPath("Wave Bank.xwb"));
            soundBankReader = new SoundBankReader(contentManager.GetPath("Sound Bank.xsb"));

            // Initialize the dictionaries to store old and new cues, and the mapping from tracks to wave indices
            oldCues = new Dictionary<int, IAudioTrack>();
            newCues = new Dictionary<int, IAudioTrack>();
            trackToWaveIndex = new Dictionary<IAudioTrack, ushort>();
            ushort waveIndex = 0;

            for (int i = 0; i < legacyAudioSystem.AudioTracks.Length; i++)
            {
                if (legacyAudioSystem.AudioTracks[i] is CueAudioTrack cueAudioTrack)
                {
                    waveIndex = (ushort)soundBankReader._cues.GetValueOrDefault(cueAudioTrack._cueName).trackIndex;
                    var track = new WaveBankAudioTrack(waveBankReader, waveIndex, cueAudioTrack._cueName);
                    oldCues.Add(i, cueAudioTrack);
                    newCues.Add(i, track);
                    trackToWaveIndex.Add(track, waveIndex);
                    cueAudioTrack.Stop(Microsoft.Xna.Framework.Audio.AudioStopOptions.Immediate);
                    legacyAudioSystem.AudioTracks[i] = track;
                }
            }

            // Initialize AudioTrackPositionTracker with the necessary data
            var trackPositionTracker = ModContent.GetInstance<AudioTrackPositionTracker>();
            trackPositionTracker.RegisterTracks(waveBankReader, trackToWaveIndex);
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
            if (Main.audioSystem is not LegacyAudioSystem legacyAudioSystem)
            {
                return;
            }

            IsTurnedOn = !IsTurnedOn;
            if (IsTurnedOn)
            {
                foreach ((int i, IAudioTrack track) in newCues)
                {
                    legacyAudioSystem.AudioTracks[i].Stop(Microsoft.Xna.Framework.Audio.AudioStopOptions.Immediate);
                    legacyAudioSystem.AudioTracks[i] = track;

                }

            }
            else
            {
                foreach ((int i, IAudioTrack track) in oldCues)
                {
                    legacyAudioSystem.AudioTracks[i].Stop(Microsoft.Xna.Framework.Audio.AudioStopOptions.Immediate);
                    legacyAudioSystem.AudioTracks[i] = track;

                }
            }

        }

    }
}
