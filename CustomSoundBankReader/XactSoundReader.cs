using System;
using System.IO;

namespace Instrumentarria.CustomSoundBankReader
{
    class XactSoundReader
    {
        private readonly bool _complexSound;
        private readonly XactClipReader[] _soundClips;
        private readonly int _waveBankIndex;
        public int trackIndex;
        private readonly float _volume;
        private readonly float _pitch;
        private readonly uint _categoryID;
        private readonly SoundBankReader _soundBank;

        private float _cueVolume;

        public XactSoundReader(SoundBankReader soundBank, int waveBankIndex, int trackIndex)
        {
            _complexSound = false;

            _soundBank = soundBank;
            _waveBankIndex = waveBankIndex;
            this.trackIndex = trackIndex;
            //Log.Info($"XactSound: {_soundBank}, {_waveBankIndex}, {_trackIndex}");
        }

        public XactSoundReader(SoundBankReader soundBank, BinaryReader soundReader)
        {
            _soundBank = soundBank;

            var flags = soundReader.ReadByte();
            _complexSound = (flags & 0x1) != 0;
            var hasRPCs = (flags & 0x0E) != 0;
            var hasEffects = (flags & 0x10) != 0;

            _categoryID = soundReader.ReadUInt16();
            _volume = XactHelpers.ParseVolumeFromDecibels(soundReader.ReadByte());
            _pitch = soundReader.ReadInt16() / 1000.0f;
            var priority = soundReader.ReadByte();
            soundReader.ReadUInt16(); // filter stuff?

            uint numClips = 0;
            if (_complexSound)
                numClips = soundReader.ReadByte();
            else
            {
                trackIndex = soundReader.ReadUInt16();
                _waveBankIndex = soundReader.ReadByte();
            }

            if (hasRPCs)
            {
                var current = soundReader.BaseStream.Position;
                var dataLength = soundReader.ReadUInt16();
                soundReader.BaseStream.Seek(current + dataLength, SeekOrigin.Begin);
            }

            if (hasEffects)
            {
                var current = soundReader.BaseStream.Position;
                var dataLength = soundReader.ReadUInt16();
                soundReader.BaseStream.Seek(current + dataLength, SeekOrigin.Begin);
            }

            if (_complexSound)
            {
                _soundClips = new XactClipReader[numClips];
                for (int i = 0; i < numClips; i++)
                    _soundClips[i] = new XactClipReader(soundBank, soundReader);
            }
            Log.Info($"XactSound: {_waveBankIndex}, {trackIndex}");
        }
    }
}


