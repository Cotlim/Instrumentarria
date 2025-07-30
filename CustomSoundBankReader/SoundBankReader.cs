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

        public Dictionary<string, CueReader> _cues = new Dictionary<string, CueReader>();

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


                    // Simple cues
                    stream.Seek(simpleCuesOffset, SeekOrigin.Begin);
                    for (int i = 0; i < numSimpleCues; i++)
                    {
                        //Log.Info($"Simple Cue: {_cueNames[i]}");
                        reader.ReadByte(); // flags
                        uint soundOffset = reader.ReadUInt32();

                        var oldPosition = stream.Position;
                        stream.Seek(soundOffset, SeekOrigin.Begin);
                        XactSoundReader sound = new XactSoundReader(this, reader);
                        stream.Seek(oldPosition, SeekOrigin.Begin);

                        CueReader cue = new CueReader(_cueNames[i], sound);
                        _cues.Add(cue.Name, cue);
                    }

                    // Complex cues

                    

                    stream.Seek(complexCuesOffset, SeekOrigin.Begin);
                    for (int i = 0; i < numComplexCues; i++)
                    {
                        //Log.Info($"Complex Cue: {_cueNames[i]}");
                        CueReader cue;

                        byte flags = reader.ReadByte();
                        if (((flags >> 2) & 1) != 0)
                        {
                            uint soundOffset = reader.ReadUInt32();
                            reader.ReadUInt32(); //unkn

                            var oldPosition = stream.Position;
                            stream.Seek(soundOffset, SeekOrigin.Begin);
                            XactSoundReader sound = new XactSoundReader(this, reader);
                            stream.Seek(oldPosition, SeekOrigin.Begin);

                            cue = new CueReader(_cueNames[numSimpleCues + i], sound);
                        }
                        else
                        {
                            uint variationTableOffset = reader.ReadUInt32();
                            reader.ReadUInt32(); // transitionTableOffset

                            //parse variation table
                            long savepos = stream.Position;
                            stream.Seek(variationTableOffset, SeekOrigin.Begin);

                            uint numEntries = reader.ReadUInt16();
                            uint variationflags = reader.ReadUInt16();
                            reader.ReadByte();
                            reader.ReadUInt16();
                            reader.ReadByte();

                            XactSoundReader[] cueSounds = new XactSoundReader[numEntries];
                            float[] probs = new float[numEntries];

                            uint tableType = (variationflags >> 3) & 0x7;
                            for (int j = 0; j < numEntries; j++)
                            {
                                switch (tableType)
                                {
                                    case 0: //Wave
                                        {
                                            int trackIndex = reader.ReadUInt16();
                                            int waveBankIndex = reader.ReadByte();
                                            reader.ReadByte(); // weightMin
                                            reader.ReadByte(); // weightMax

                                            cueSounds[j] = new XactSoundReader(this, waveBankIndex, trackIndex);
                                            break;
                                        }
                                    case 1:
                                        {
                                            uint soundOffset = reader.ReadUInt32();
                                            reader.ReadByte(); // weightMin
                                            reader.ReadByte(); // weightMax

                                            var oldPosition = stream.Position;
                                            stream.Seek(soundOffset, SeekOrigin.Begin);
                                            cueSounds[j] = new XactSoundReader(this, reader);
                                            stream.Seek(oldPosition, SeekOrigin.Begin);
                                            break;
                                        }
                                    case 3:
                                        {
                                            uint soundOffset = reader.ReadUInt32();
                                            var weightMin = reader.ReadSingle();
                                            var weightMax = reader.ReadSingle();
                                            var varFlags = reader.ReadUInt32();
                                            var linger = (varFlags & 0x01) == 0x01;

                                            var oldPosition = stream.Position;
                                            stream.Seek(soundOffset, SeekOrigin.Begin);
                                            cueSounds[j] = new XactSoundReader(this, reader);
                                            stream.Seek(oldPosition, SeekOrigin.Begin);
                                            break;
                                        }
                                    case 4: //CompactWave
                                        {
                                            int trackIndex = reader.ReadUInt16();
                                            int waveBankIndex = reader.ReadByte();
                                            cueSounds[j] = new XactSoundReader(this, waveBankIndex, trackIndex);
                                            break;
                                        }
                                    default:
                                        throw new NotSupportedException();
                                }
                            }

                            stream.Seek(savepos, SeekOrigin.Begin);

                            cue = new CueReader(_cueNames[numSimpleCues + i], cueSounds, probs);
                        }

                        // Instance limiting
                        var instanceLimit = reader.ReadByte();
                        var fadeInSec = reader.ReadUInt16() / 1000.0f;
                        var fadeOutSec = reader.ReadUInt16() / 1000.0f;
                        var instanceFlags = reader.ReadByte();

                        _cues.Add(cue.Name, cue);
                    }
                }
            }


        }
    }
}


