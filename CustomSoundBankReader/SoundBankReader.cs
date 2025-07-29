using Microsoft.Xna.Framework;
using MonoGame.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Instrumentarria.CustomSoundBankReader
{
    // Unused SB reader
    public class SoundBankReader
    {
        public string[] _waveBankNames;
        public string[] _cueNames;

        public bool IsDisposed { get; private set; }

        /// <param name="fileName">Path to a .xsb SoundBank file.</param>
        public SoundBankReader(string fileName)
        {
            fileName = FileHelpers.NormalizeFilePathSeparators(fileName);

            using (var stream = TitleContainer.OpenStream(fileName))
            {

                using (var reader = new BinaryReader(stream))
                {
                    // Thanks to Liandril for "xactxtract" for some of the offsets.

                    uint magic = reader.ReadUInt32();
                    if (magic != 0x4B424453) //"SDBK"
                        throw new Exception("Bad soundbank format");

                    reader.ReadUInt16(); // toolVersion

                    uint formatVersion = reader.ReadUInt16();
                    if (formatVersion != 46)
                    {
                        Log.Warn($"Warning: SoundBank format {formatVersion} not supported.");
                    }


                    reader.ReadUInt16(); // crc, TODO: Verify crc (FCS16)

                    reader.ReadUInt32(); // lastModifiedLow
                    reader.ReadUInt32(); // lastModifiedHigh
                    reader.ReadByte(); // platform ???

                    uint numSimpleCues = reader.ReadUInt16();
                    uint numComplexCues = reader.ReadUInt16();
                    reader.ReadUInt16(); //unkn
                    reader.ReadUInt16(); // numTotalCues
                    uint numWaveBanks = reader.ReadByte();
                    reader.ReadUInt16(); // numSounds
                    uint cueNameTableLen = reader.ReadUInt16();
                    reader.ReadUInt16(); //unkn

                    uint simpleCuesOffset = reader.ReadUInt32();
                    uint complexCuesOffset = reader.ReadUInt32(); //unkn
                    uint cueNamesOffset = reader.ReadUInt32();
                    reader.ReadUInt32(); //unkn
                    reader.ReadUInt32(); // variationTablesOffset
                    reader.ReadUInt32(); //unkn
                    uint waveBankNameTableOffset = reader.ReadUInt32();
                    reader.ReadUInt32(); // cueNameHashTableOffset
                    reader.ReadUInt32(); // cueNameHashValsOffset
                    reader.ReadUInt32(); // soundsOffset

                    //name = System.Text.Encoding.UTF8.GetString(soundbankreader.ReadBytes(64),0,64).Replace("\0","");

                    //parse wave bank name table
                    stream.Seek(waveBankNameTableOffset, SeekOrigin.Begin);
                    _waveBankNames = new string[numWaveBanks];
                    for (int i = 0; i < numWaveBanks; i++)
                        _waveBankNames[i] = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(64), 0, 64).Replace("\0", "");

                    //parse cue name table
                    stream.Seek(cueNamesOffset, SeekOrigin.Begin);
                    _cueNames = System.Text.Encoding.UTF8.GetString(reader.ReadBytes((int)cueNameTableLen), 0, (int)cueNameTableLen).Split('\0');
                }
            }

        }
    }
}


