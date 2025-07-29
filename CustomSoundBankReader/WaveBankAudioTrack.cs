using Microsoft.Xna.Framework.Audio;
using System;
using System.IO;
using System.Net.Security;
using Terraria.Audio;

namespace Instrumentarria.CustomSoundBankReader
{

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

            public WaveBankAudioTrack(WaveBankReader reader, ushort waveIndex)
            {
                _reader = reader;
                _waveIndex = waveIndex;

                var entry = _reader.GetEntry(_waveIndex);
                _stream = _reader.CreateStreamForEntry(_waveIndex);
                _readerStream = new BinaryReader(_stream);

                CreateSoundEffect(entry.SampleRate, entry.Channels);
            }

            public override void ReadAheadPutAChunkIntoTheBuffer()
            {
                int read = _stream.Read(_bufferToSubmit, 0, _bufferToSubmit.Length);
                if (read == 0)
                {
                    Stop(AudioStopOptions.Immediate);
                    return;
                }

                _soundEffectInstance.SubmitBuffer(_bufferToSubmit, 0, read);
            }

            public override void Reuse()
            {
                _stream?.Dispose();
                _stream = _reader.CreateStreamForEntry(_waveIndex);
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

}


