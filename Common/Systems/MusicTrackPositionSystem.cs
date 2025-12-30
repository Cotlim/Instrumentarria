using Instrumentarria.CustomSoundBankReader;
using Instrumentarria.Helpers;
using System;
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
        private Dictionary<IAudioTrack, ushort> trackToWaveIndex;
        private int _logCounter = 0;

        public override void OnModLoad()
        {
            trackToWaveIndex = new Dictionary<IAudioTrack, ushort>();
        }

        public override void OnModUnload()
        {
            trackToWaveIndex?.Clear();
            trackToWaveIndex = null;
            waveBankReader = null;
        }

        /// <summary>
        /// Initializes the system with the necessary readers and mappings.
        /// Should be called by ReplaceAllCuesSystem after it sets up the audio tracks.
        /// </summary>
        /// <param name="waveBankReader">The wave bank reader to use for track data</param>
        /// <param name="trackToWaveIndex">Dictionary mapping audio tracks to wave indices</param>
        public void Initialize(WaveBankReader waveBankReader, Dictionary<IAudioTrack, ushort> trackToWaveIndex)
        {
            this.waveBankReader = waveBankReader;
            this.trackToWaveIndex = trackToWaveIndex;
        }

        /// <summary>
        /// Gets the current playback position of an audio track in seconds.
        /// </summary>
        /// <param name="track">The audio track to check</param>
        /// <returns>The current position in seconds, or 0 if unable to determine</returns>
        public double GetTrackPosition(IAudioTrack track)
        {
            if (track == null)
                return 0;

            // Try to get position based on track type
            return track switch
            {
                WaveBankAudioTrack waveBankTrack => GetWaveBankTrackPosition(waveBankTrack),
                MP3AudioTrack mp3Track => GetMP3TrackPosition(mp3Track),
                OGGAudioTrack oggTrack => GetOGGTrackPosition(oggTrack),
                WAVAudioTrack wavTrack => GetWAVTrackPosition(wavTrack),
                _ => new Func<double>(() =>
                {
                    Log.Warn($"Unsupported track type: {track.GetType().Name}");
                    return 0;
                })()
            };
        }

        /// <summary>
        /// Gets the position of a WaveBankAudioTrack.
        /// </summary>
        private double GetWaveBankTrackPosition(WaveBankAudioTrack track)
        {
            if (waveBankReader == null)
                return 0;

            if (!trackToWaveIndex.TryGetValue(track, out var waveIndex))
                return 0;

            var stream = track._stream;
            if (stream == null)
                return 0;

            long positionBytes = stream.Position;
            var entry = waveBankReader.GetEntry(waveIndex);

            int bytesPerSample = 2 * (int)entry.Channels;
            double positionSamples = positionBytes / (double)bytesPerSample;
            double positionSeconds = positionSamples / entry.SampleRate;

            return positionSeconds;
        }

        /// <summary>
        /// Gets the position of an MP3AudioTrack.
        /// </summary>
        private double GetMP3TrackPosition(MP3AudioTrack track)
        {
            var mp3Stream = track._mp3Stream;
            if (mp3Stream == null)
                return 0;

            // MP3Stream.Position is in bytes, need to convert to seconds
            long positionBytes = mp3Stream.Position;
            int sampleRate = mp3Stream.Frequency;
            int channels = 2; // MP3AudioTrack always uses stereo
            int bytesPerSample = 2 * channels; // 16-bit stereo

            double positionSamples = positionBytes / (double)bytesPerSample;
            double positionSeconds = positionSamples / sampleRate;

            return positionSeconds;
        }

        /// <summary>
        /// Gets the position of an OGGAudioTrack.
        /// </summary>
        private double GetOGGTrackPosition(OGGAudioTrack track)
        {
            var vorbisReader = track._vorbisReader;
            if (vorbisReader == null)
                return 0;

            // VorbisReader.SamplePosition returns sample count
            long decodedSamples = vorbisReader.SamplePosition;
            int sampleRate = vorbisReader.SampleRate;

            double positionSeconds = decodedSamples / (double)sampleRate;

            return positionSeconds;
        }

        /// <summary>
        /// Gets the position of a WAVAudioTrack.
        /// </summary>
        private double GetWAVTrackPosition(WAVAudioTrack track)
        {
            var stream = track._stream;
            if (stream == null)
                return 0;

            long streamContentStart = track._streamContentStartIndex;

            // Get sample rate and channels from base class fields
            int sampleRate = track._sampleRate;
            var channels = track._channels;

            // Calculate position
            long positionBytes = stream.Position - streamContentStart;
            if (positionBytes < 0)
                return 0;

            // WAV is 16-bit PCM
            int channelCount = (int)channels;
            int bytesPerSample = 2 * channelCount;

            double positionSamples = positionBytes / (double)bytesPerSample;
            double positionSeconds = positionSamples / sampleRate;

            return positionSeconds;
        }

        /// <summary>
        /// Gets the current playback position of a music track in seconds.
        /// </summary>
        /// <param name="musicSlot">The music slot to check</param>
        /// <returns>The current position in seconds, or 0 if unable to determine</returns>
        public double GetTrackPosition(int musicSlot)
        {
            if (Main.audioSystem is not LegacyAudioSystem legacyAudioSystem)
                return 0;

            if (musicSlot < 0 || musicSlot >= legacyAudioSystem.AudioTracks.Length)
                return 0;

            return GetTrackPosition(legacyAudioSystem.AudioTracks[musicSlot]);
        }
    }
}
