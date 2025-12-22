using Instrumentarria.CustomSoundBankReader;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace Instrumentarria.Common.Systems
{
    /// <summary>
    /// System responsible for tracking and retrieving the current playback position of music tracks.
    /// Separated from ReplaceAllCuesSystem for better separation of concerns.
    /// </summary>
    internal class MusicTrackPositionSystem : ModSystem
    {
        private WaveBankReader waveBankReader;
        private Dictionary<int, ushort> musicSlotToWaveIndex;

        public override void OnModLoad()
        {
            musicSlotToWaveIndex = new Dictionary<int, ushort>();
        }

        public override void OnModUnload()
        {
            musicSlotToWaveIndex?.Clear();
            musicSlotToWaveIndex = null;
            waveBankReader = null;
        }

        /// <summary>
        /// Initializes the system with the necessary readers and mappings.
        /// Should be called by ReplaceAllCuesSystem after it sets up the audio tracks.
        /// </summary>
        /// <param name="waveBankReader">The wave bank reader to use for track data</param>
        /// <param name="musicSlotToWaveIndex">Dictionary mapping music slots to wave indices</param>
        public void Initialize(WaveBankReader waveBankReader, Dictionary<int, ushort> musicSlotToWaveIndex)
        {
            this.waveBankReader = waveBankReader;
            this.musicSlotToWaveIndex = musicSlotToWaveIndex;
        }

        /// <summary>
        /// Gets the current playback position of a music track in seconds.
        /// </summary>
        /// <param name="musicSlot">The music slot to check</param>
        /// <returns>The current position in seconds, or 0 if unable to determine</returns>
        public double GetTrackPosition(int musicSlot)
        {
            if (waveBankReader == null)
                return 0;

            if (Main.audioSystem is not LegacyAudioSystem legacyAudioSystem)
                return 0;

            if (musicSlot < 0 || musicSlot >= legacyAudioSystem.AudioTracks.Length)
                return 0;

            var track = legacyAudioSystem.AudioTracks[musicSlot];
            
            if (track is not WaveBankAudioTrack waveBankTrack)
                return 0;

            if (!musicSlotToWaveIndex.TryGetValue(musicSlot, out var waveIndex))
                return 0;

            var stream = waveBankTrack._stream;
            if (stream == null)
                return 0;

            long positionBytes = stream.Position;
            var entry = waveBankReader.GetEntry(waveIndex);
            
            int bytesPerSample = 2 * (int)entry.Channels;
            double positionSamples = positionBytes / (double)bytesPerSample;
            double positionSeconds = positionSamples / entry.SampleRate;

            return positionSeconds;
        }
    }
}
