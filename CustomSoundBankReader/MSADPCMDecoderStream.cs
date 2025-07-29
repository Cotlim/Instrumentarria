using Mono.Cecil;
using System;
using System.IO;
using Terraria.ModLoader.Engine;

namespace Instrumentarria.CustomSoundBankReader
{
    // A stream that automatily decodes MSADPCM into normal format
    public class MSADPCMDecodedStream : Stream
    {
        private Stream _compressedStream;
        private long _decodedPosition;

        public const int CompressedBlockSize = 140; // 14 + ("48"+15)*2
        public const int DecompressedBlockSize = 512; // 8 + ("48"+15)*2*2*2

        public MSADPCMDecodedStream(Stream compressedStream)
        {
            _compressedStream = compressedStream;

            _decodedPosition = 0;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => (_compressedStream.Length / CompressedBlockSize) * DecompressedBlockSize;
        public override long Position
        {
            get => _decodedPosition;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // Calculate a target box to unpack
            int startBlockIndex = (int)(_decodedPosition / DecompressedBlockSize);
            long targetCompressedPos = startBlockIndex * CompressedBlockSize;

            // Calculate an offset
            int offsetOfDecodedPosToEncodedBlocks = (int)(_decodedPosition - startBlockIndex * DecompressedBlockSize);

            Span<byte> decodedBlock = buffer;
            int bytesDecodedTotal = 0;

            byte[] currentBlock = new byte[CompressedBlockSize];
            if (targetCompressedPos >= _compressedStream.Length)
            {
                return 0;
            }

            _compressedStream.Position = targetCompressedPos;

            while (bytesDecodedTotal < count + offsetOfDecodedPosToEncodedBlocks)
            {
                int read = _compressedStream.Read(currentBlock, 0, CompressedBlockSize);
                if (read < CompressedBlockSize)
                {
                    if (bytesDecodedTotal > 0)
                    {
                        break; // виходимо з циклу і віддамо все, що є
                    }

                    // Кінець потоку або неповний блок — сигналізуємо, що немає що декодувати
                    return 0;
                }

                int decodedBytes = MSADPCMDecoder.DecodeBlock(currentBlock, decodedBlock.Slice(bytesDecodedTotal));
                bytesDecodedTotal += decodedBytes;
            }

            // Реальна кількість байтів, яку ми можемо повернути з урахуванням offset
            int availableDecoded = bytesDecodedTotal - offsetOfDecodedPosToEncodedBlocks;
            int actualToCopy = Math.Min(count, availableDecoded);

            decodedBlock.Slice(offsetOfDecodedPosToEncodedBlocks, actualToCopy).CopyTo(buffer);

            _decodedPosition += actualToCopy;
            return actualToCopy;

        }

        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin)
        {
            long target = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _decodedPosition + offset,
                SeekOrigin.End => Length + offset,
                _ => throw new ArgumentOutOfRangeException()
            };

            if (target < 0 || target > Length)
                throw new IOException("Seek out of bounds");

            _decodedPosition = target;
            return _decodedPosition;
        }

        protected override void Dispose(bool disposing)
        {
            _compressedStream?.Dispose();
            _compressedStream = null;
            base.Dispose(disposing);
        }
    }

}
