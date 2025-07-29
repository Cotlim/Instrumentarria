// Simplified SoundBank parser for reading cue names from .xsb
// Stripped of all runtime/audio functionality — only file parsing remains

using MonoGame.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Terraria;
using Terraria.ModLoader.Engine;

namespace Instrumentarria.Helpers
{
    public class ParsedCue
    {
        public string Name;
        // You can add more fields later if needed
    }

    //TODO: turn this into SB reader for sorting new cues in right way
    public class SimpleSoundBank
    {
        public List<ParsedCue> Cues = new();
        public List<string> WaveBankNames = new();

        public SimpleSoundBank(string fileName)
        {
            var contentManager = (TMLContentManager)Main.instance.Content;

            var filePath = FileHelpers.NormalizeFilePathSeparators(contentManager.GetPath(fileName));

            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryReader(stream);

            uint magic = reader.ReadUInt32();
            if (magic != 0x4B424453) // "SDBK"
                throw new Exception("Invalid SoundBank file.");

            reader.ReadUInt16(); // toolVersion
            uint formatVersion = reader.ReadUInt16();
            if (formatVersion != 46)
                Console.WriteLine($"Warning: unexpected XSB format version: {formatVersion}");

            reader.ReadUInt16(); // crc
            reader.ReadUInt32(); // lastModifiedLow
            reader.ReadUInt32(); // lastModifiedHigh
            reader.ReadByte();   // platform

            ushort numSimpleCues = reader.ReadUInt16();
            ushort numComplexCues = reader.ReadUInt16();
            reader.ReadUInt16(); // unknown
            reader.ReadUInt16(); // total cues
            byte numWaveBanks = reader.ReadByte();
            reader.ReadUInt16(); // numSounds
            ushort cueNameTableLen = reader.ReadUInt16();
            reader.ReadUInt16(); // unknown

            uint simpleCuesOffset = reader.ReadUInt32();
            uint complexCuesOffset = reader.ReadUInt32();
            uint cueNamesOffset = reader.ReadUInt32();
            reader.ReadUInt32(); // unused
            reader.ReadUInt32(); // variationTablesOffset
            reader.ReadUInt32(); // unused
            uint waveBankNameTableOffset = reader.ReadUInt32();
            reader.ReadUInt32(); // cueNameHashTableOffset
            reader.ReadUInt32(); // cueNameHashValsOffset
            reader.ReadUInt32(); // soundsOffset

            // Read wave bank names
            stream.Seek(waveBankNameTableOffset, SeekOrigin.Begin);
            for (int i = 0; i < numWaveBanks; i++)
            {
                var name = Encoding.UTF8.GetString(reader.ReadBytes(64)).TrimEnd('\0');
                WaveBankNames.Add(name);
            }

            // Read cue names
            stream.Seek(cueNamesOffset, SeekOrigin.Begin);
            var cueNameBytes = reader.ReadBytes(cueNameTableLen);
            var cueNames = Encoding.UTF8.GetString(cueNameBytes).Split('\0');

            for (int i = 0; i < cueNames.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(cueNames[i]))
                    Cues.Add(new ParsedCue { Name = cueNames[i] });
            }
        }
    }
}
