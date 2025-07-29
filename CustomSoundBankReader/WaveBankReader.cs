using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading.Channels;

namespace Instrumentarria.CustomSoundBankReader
{
    public class WaveBankReader : IDisposable
    {
        struct Segment
        {
            public int Offset;
            public int Length;
        }

        struct WaveBankEntry
        {
            public int Format;
            public Segment PlayRegion;
            public Segment LoopRegion;
            public int FlagsAndDuration;
        }

        struct WaveBankHeader
        {
            public int Version;
            public Segment[] Segments;
        }

        struct WaveBankData
        {
            public int Flags;                                // Bank flags
            public int EntryCount;                           // Number of entries in the bank
            public string BankName;                             // Bank friendly name
            public int EntryMetaDataElementSize;             // Size of each entry meta-data element, in bytes
            public int EntryNameElementSize;                 // Size of each entry name element, in bytes
            public int Alignment;                            // Entry alignment, in bytes
            public int CompactFormat;                        // Format data for compact bank
            public int BuildTime;                            // Build timestamp
        }



        public struct WaveEntryInfo
        {
            public long Offset;           // Початкова позиція хвилі в .xwb
            public long Length;           // Довжина хвилі (в байтах)

            public int SampleRate;        // WAVEFORMATEX.nSamplesPerSec
            public AudioChannels Channels;// WAVEFORMATEX.nChannels

            public int BlockAlign;      // WAVEFORMATEX.nBlockAlign
            public AudioFormat Format;    // Наш enum — PCM, ADPCM, тощо

            public bool IsMSADPCM => Format == AudioFormat.MSADPCM;

        }
        public enum AudioFormat : ushort
        {
            PCM = 0x0001,
            MSADPCM = 0x0002,
            XMA = 0x0165,   // якщо знадобиться
            WMA = 0x0161,
            Unknown = 0xFFFF
        }


        private readonly Dictionary<int, WaveEntryInfo> _entries = new();
        private readonly string _filePath;
        private string _bankName;
        private long _waveDataOffset;





        private const int Flag_EntryNames = 0x00010000; // Bank includes entry names
        private const int Flag_Compact = 0x00020000; // Bank uses compact format
        private const int Flag_SyncDisabled = 0x00040000; // Bank is disabled for audition sync
        private const int Flag_SeekTables = 0x00080000; // Bank includes seek tables.
        private const int Flag_Mask = 0x000F0000;
        private const int MiniFormatTag_PCM = 0x0;
        private const int MiniFormatTag_XMA = 0x1;
        private const int MiniFormatTag_ADPCM = 0x2;
        private const int MiniForamtTag_WMA = 0x3;
        public WaveBankReader(string filePath)
        {
            WaveBankHeader wavebankheader;
            WaveBankData wavebankdata;
            WaveBankEntry wavebankentry;

            wavebankdata.EntryNameElementSize = 0;
            wavebankdata.CompactFormat = 0;
            wavebankdata.Alignment = 0;
            wavebankdata.BuildTime = 0;

            wavebankentry.Format = 0;
            wavebankentry.PlayRegion.Length = 0;
            wavebankentry.PlayRegion.Offset = 0;

            int wavebank_offset = 0;

            _filePath = filePath;
            using var xwbStream = File.OpenRead(filePath);
            BinaryReader reader = new BinaryReader(xwbStream);

            reader.ReadBytes(4);

            wavebankheader.Version = reader.ReadInt32();

            int last_segment = 4;
            //if (wavebankheader.Version == 1) goto WAVEBANKDATA;
            if (wavebankheader.Version <= 3) last_segment = 3;
            if (wavebankheader.Version >= 42) reader.ReadInt32();    // skip HeaderVersion

            wavebankheader.Segments = new Segment[5];

            for (int i = 0; i <= last_segment; i++)
            {
                wavebankheader.Segments[i].Offset = reader.ReadInt32();
                wavebankheader.Segments[i].Length = reader.ReadInt32();
            }

            reader.BaseStream.Seek(wavebankheader.Segments[0].Offset, SeekOrigin.Begin);

            //WAVEBANKDATA:

            wavebankdata.Flags = reader.ReadInt32();
            wavebankdata.EntryCount = reader.ReadInt32();

            if ((wavebankheader.Version == 2) || (wavebankheader.Version == 3))
            {
                wavebankdata.BankName = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(16), 0, 16).Replace("\0", "");
            }
            else
            {
                wavebankdata.BankName = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(64), 0, 64).Replace("\0", "");
            }

            _bankName = wavebankdata.BankName;

            if (wavebankheader.Version == 1)
            {
                //wavebank_offset = (int)ftell(fd) - file_offset;
                wavebankdata.EntryMetaDataElementSize = 20;
            }
            else
            {
                wavebankdata.EntryMetaDataElementSize = reader.ReadInt32();
                wavebankdata.EntryNameElementSize = reader.ReadInt32();
                wavebankdata.Alignment = reader.ReadInt32();
                wavebank_offset = wavebankheader.Segments[1].Offset; //METADATASEGMENT
            }

            if ((wavebankdata.Flags & Flag_Compact) != 0)
            {
                reader.ReadInt32(); // compact_format
            }

            int playregion_offset = wavebankheader.Segments[last_segment].Offset;
            if (playregion_offset == 0)
            {
                playregion_offset =
                    wavebank_offset +
                    (wavebankdata.EntryCount * wavebankdata.EntryMetaDataElementSize);
            }

            int segidx_entry_name = 2;
            if (wavebankheader.Version >= 42) segidx_entry_name = 3;

            if ((wavebankheader.Segments[segidx_entry_name].Offset != 0) &&
                (wavebankheader.Segments[segidx_entry_name].Length != 0))
            {
                if (wavebankdata.EntryNameElementSize == -1) wavebankdata.EntryNameElementSize = 0;
                byte[] entry_name = new byte[wavebankdata.EntryNameElementSize + 1];
                entry_name[wavebankdata.EntryNameElementSize] = 0;
            }


            for (int current_entry = 0; current_entry < wavebankdata.EntryCount; current_entry++)
            {

                reader.BaseStream.Seek(wavebank_offset, SeekOrigin.Begin);
                //SHOWFILEOFF;

                //memset(&wavebankentry, 0, sizeof(wavebankentry));
                wavebankentry.LoopRegion.Length = 0;
                wavebankentry.LoopRegion.Offset = 0;

                if ((wavebankdata.Flags & Flag_Compact) != 0)
                {
                    int len = reader.ReadInt32();
                    wavebankentry.Format = wavebankdata.CompactFormat;
                    wavebankentry.PlayRegion.Offset = (len & ((1 << 21) - 1)) * wavebankdata.Alignment;
                    wavebankentry.PlayRegion.Length = (len >> 21) & ((1 << 11) - 1);

                    // workaround because I don't know how to handke the deviation length
                    reader.BaseStream.Seek(wavebank_offset + wavebankdata.EntryMetaDataElementSize, SeekOrigin.Begin);

                    if (current_entry == (wavebankdata.EntryCount - 1))
                    {              // the last track
                        len = wavebankheader.Segments[last_segment].Length;
                    }
                    else
                    {
                        len = ((reader.ReadInt32() & ((1 << 21) - 1)) * wavebankdata.Alignment);
                    }
                    wavebankentry.PlayRegion.Length =
                        len -                               // next offset
                        wavebankentry.PlayRegion.Offset;  // current offset
                    goto wavebank_handle;
                }

                if (wavebankheader.Version == 1)
                {
                    wavebankentry.Format = reader.ReadInt32();
                    wavebankentry.PlayRegion.Offset = reader.ReadInt32();
                    wavebankentry.PlayRegion.Length = reader.ReadInt32();
                    wavebankentry.LoopRegion.Offset = reader.ReadInt32();
                    wavebankentry.LoopRegion.Length = reader.ReadInt32();
                }
                else
                {
                    if (wavebankdata.EntryMetaDataElementSize >= 4) wavebankentry.FlagsAndDuration = reader.ReadInt32();
                    if (wavebankdata.EntryMetaDataElementSize >= 8) wavebankentry.Format = reader.ReadInt32();
                    if (wavebankdata.EntryMetaDataElementSize >= 12) wavebankentry.PlayRegion.Offset = reader.ReadInt32();
                    if (wavebankdata.EntryMetaDataElementSize >= 16) wavebankentry.PlayRegion.Length = reader.ReadInt32();
                    if (wavebankdata.EntryMetaDataElementSize >= 20) wavebankentry.LoopRegion.Offset = reader.ReadInt32();
                    if (wavebankdata.EntryMetaDataElementSize >= 24) wavebankentry.LoopRegion.Length = reader.ReadInt32();
                }

                if (wavebankdata.EntryMetaDataElementSize < 24)
                {                              // work-around
                    if (wavebankentry.PlayRegion.Length != 0)
                    {
                        wavebankentry.PlayRegion.Length = wavebankheader.Segments[last_segment].Length;
                    }

                }

            wavebank_handle:
                wavebank_offset += wavebankdata.EntryMetaDataElementSize;
                wavebankentry.PlayRegion.Offset += playregion_offset;

                int codec;
                int chans;
                int rate;
                int align;

                if (wavebankheader.Version == 1)
                {
                    codec = (wavebankentry.Format) & ((1 << 1) - 1);
                    chans = (wavebankentry.Format >> (1)) & ((1 << 3) - 1);
                    rate = (wavebankentry.Format >> (1 + 3 + 1)) & ((1 << 18) - 1);
                    align = (wavebankentry.Format >> (1 + 3 + 1 + 18)) & ((1 << 8) - 1);
                }
                else
                {
                    codec = (wavebankentry.Format) & ((1 << 2) - 1);
                    chans = (wavebankentry.Format >> (2)) & ((1 << 3) - 1);
                    rate = (wavebankentry.Format >> (2 + 3)) & ((1 << 18) - 1);
                    align = (wavebankentry.Format >> (2 + 3 + 18)) & ((1 << 8) - 1);
                }

                reader.BaseStream.Seek(wavebankentry.PlayRegion.Offset, SeekOrigin.Begin);
                byte[] audiodata = reader.ReadBytes(wavebankentry.PlayRegion.Length);

                if (codec == MiniFormatTag_PCM)
                {
                    _entries[current_entry] = new WaveEntryInfo
                    {
                        Offset = wavebankentry.PlayRegion.Offset,
                        Length = wavebankentry.PlayRegion.Length,
                        SampleRate = rate,
                        Channels = (AudioChannels)chans,
                        Format = AudioFormat.PCM,
                    };
                }
                else if (codec == MiniForamtTag_WMA)
                {
                    throw new NotImplementedException();
                }
                else if (codec == MiniFormatTag_ADPCM)
                {
                    _entries[current_entry] = new WaveEntryInfo
                    {
                        Offset = wavebankentry.PlayRegion.Offset,
                        Length = wavebankentry.PlayRegion.Length,
                        SampleRate = rate,
                        Channels = (AudioChannels)chans,
                        Format = AudioFormat.MSADPCM,
                        BlockAlign = align
                    };
                }
                else
                {
                    Log.Info("Error");
                    throw new NotImplementedException();
                }

            }
        }

        public WaveEntryInfo GetEntry(ushort index) => _entries[index];

        public Stream CreateStreamForEntry(ushort index)
        {
            var info = GetEntry(index);
            return new SubStreamFromFile(_filePath, info.Offset, info.Length);
        }


        public void Dispose()
        {
            // Немає ресурсів, які треба вручну закривати.
        }
    }
}
