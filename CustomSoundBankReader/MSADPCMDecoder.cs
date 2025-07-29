using Mono.Cecil;
using System;
using System.IO;

namespace Instrumentarria.CustomSoundBankReader
{
    public class MSADPCMDecoder
    {
        /**
         * A bunch of magical numbers that predict the sample data from the
         * MSADPCM wavedata. Do not attempt to understand at all costs!
         */
        private static readonly int[] AdaptionTable = {
            230, 230, 230, 230, 307, 409, 512, 614,
            768, 614, 512, 409, 307, 230, 230, 230
        };
        private static readonly int[] AdaptCoeff_1 = {
            256, 512, 0, 192, 240, 460, 392
        };
        private static readonly int[] AdaptCoeff_2 = {
            0, -256, 0, 64, 0, -208, -232
        };

        /**
         * Calculates PCM samples based on previous samples and a nibble input.
         * @param nibble A parsed MSADPCM sample we got from getNibbleBlock
         * @param predictor The predictor we get from the MSADPCM block's preamble
         * @param sample_1 The first sample we use to predict the next sample
         * @param sample_2 The second sample we use to predict the next sample
         * @param delta Used to calculate the final sample
         * @return The calculated PCM sample
         */
        private static short calculateSample(
            byte nibble,
            byte predictor,
            ref short sample_1,
            ref short sample_2,
            ref short delta
        )
        {
            // Get a signed number out of the nibble. We need to retain the
            // original nibble value for when we access AdaptionTable[].
            sbyte signedNibble = (sbyte)nibble;
            if ((signedNibble & 0x8) == 0x8)
            {
                signedNibble -= 0x10;
            }

            // Calculate new sample
            int sampleInt = (
                ((sample_1 * AdaptCoeff_1[predictor]) +
                    (sample_2 * AdaptCoeff_2[predictor])
                ) / 256
            );
            sampleInt += signedNibble * delta;

            // Clamp result to 16-bit
            short sample;
            if (sampleInt < short.MinValue)
            {
                sample = short.MinValue;
            }
            else if (sampleInt > short.MaxValue)
            {
                sample = short.MaxValue;
            }
            else
            {
                sample = (short)sampleInt;
            }

            // Shuffle samples, get new delta
            sample_2 = sample_1;
            sample_1 = sample;
            delta = (short)(AdaptionTable[nibble] * delta / 256);

            // Saturate the delta to a lower bound of 16
            if (delta < 16)
                delta = 16;

            return sample;
        }


        private static void WriteShortLE(short value, Span<byte> buffer, ref int position)
        {
            buffer[position++] = (byte)(value & 0xFF);
            buffer[position++] = (byte)((value >> 8) & 0xFF);
        }

        internal static int DecodeBlock(Span<byte> rawBlock, Span<byte> decodedBlock)
        {
            byte l_predictor = rawBlock[0];
            byte r_predictor = rawBlock[1];

            short l_delta = BitConverter.ToInt16(rawBlock.Slice(2, 2));
            short r_delta = BitConverter.ToInt16(rawBlock.Slice(4, 2));

            short l_sample_1 = BitConverter.ToInt16(rawBlock.Slice(6, 2));
            short r_sample_1 = BitConverter.ToInt16(rawBlock.Slice(8, 2));
            short l_sample_2 = BitConverter.ToInt16(rawBlock.Slice(10, 2));
            short r_sample_2 = BitConverter.ToInt16(rawBlock.Slice(12, 2));

            int writePos = 0;

            // Записуємо початкові семпли (2 семпли × 2 канали)
            WriteShortLE(l_sample_2, decodedBlock, ref writePos);
            WriteShortLE(r_sample_2, decodedBlock, ref writePos);
            WriteShortLE(l_sample_1, decodedBlock, ref writePos);
            WriteShortLE(r_sample_1, decodedBlock, ref writePos);

            int blockAlign = rawBlock.Length;
            int offset = 14;

            for (int i = 0; i < (blockAlign - 14); i++)
            {
                byte b = rawBlock[offset + i];
                byte nibbleLeft = (byte)(b >> 4);
                byte nibbleRight = (byte)(b & 0xF);

                short sampleLeft = calculateSample(nibbleLeft, l_predictor, ref l_sample_1, ref l_sample_2, ref l_delta);
                short sampleRight = calculateSample(nibbleRight, r_predictor, ref r_sample_1, ref r_sample_2, ref r_delta);

                WriteShortLE(sampleLeft, decodedBlock, ref writePos);
                WriteShortLE(sampleRight, decodedBlock, ref writePos);
            }

            return writePos;
        }

    }
}
