using Microsoft.Xna.Framework.Audio;
using System;
using System.IO;
using Terraria.Audio;

namespace Instrumentarria.CustomSoundBankReader
{
    public class WaveBankAudioTrack : ASoundEffectBasedAudioTrack
    {
        private readonly WaveBankReader _reader;
        private readonly ushort _waveIndex;
        private Stream _stream;
        private BinaryReader _readerStream;
        public string _name;


        public WaveBankAudioTrack(WaveBankReader reader, ushort waveIndex, string name)
        {
            _reader = reader;
            _waveIndex = waveIndex;
            _name = name;
            var entry = _reader.GetEntry(_waveIndex);
            _stream = new MSADPCMDecodedStream(_reader.CreateStreamForEntry(_waveIndex));
            _readerStream = new BinaryReader(_stream);
            CreateSoundEffect(entry.SampleRate, entry.Channels);

        }

        public override void ReadAheadPutAChunkIntoTheBuffer()
        {
            byte[] bufferToSubmit = _bufferToSubmit;
            if (_stream.Read(bufferToSubmit, 0, bufferToSubmit.Length) < 1)
                Stop(AudioStopOptions.Immediate);
            else
                _soundEffectInstance.SubmitBuffer(_bufferToSubmit);
        }



        public override void Reuse()
        {
            _stream?.Dispose();
            _stream = new MSADPCMDecodedStream(_reader.CreateStreamForEntry(_waveIndex));
            _readerStream = new BinaryReader(_stream);
        }

        public override void Dispose()
        {
            _soundEffectInstance?.Dispose();
            _stream?.Dispose();
            _readerStream?.Dispose();
        }
    }
}
