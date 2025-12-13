using ReLogic.Content;
using ReLogic.Content.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Instrumentarria.MidiReader
{
    internal class MidiFileReader : IAssetReader
    {
        public int Format;
        public int TicksPerQuarterNote;
        public MidiTrack[] Tracks;
        public int TracksCount;

        public T FromStream<T>(Stream stream) where T : class
        {
            if (typeof(T) != typeof(MidiFile))
                throw AssetLoadException.FromInvalidReader<MidiFileReader, T>();

            var data = Reader.ReadAllBytesFromStream(stream);
            var position = 0;

            if (Reader.ReadString(data, ref position, 4) != "MThd")
                throw new FormatException("Invalid file header (expected MThd)");

            if (Reader.Read32(data, ref position) != 6)
                throw new FormatException("Invalid header length (expected 6)");

            Format = Reader.Read16(data, ref position);
            TracksCount = Reader.Read16(data, ref position);
            TicksPerQuarterNote = Reader.Read16(data, ref position);

            // SMPTE (bit 15 set) не підтримуємо
            if ((TicksPerQuarterNote & 0x8000) != 0)
                throw new FormatException("Invalid timing mode (SMPTE timecode not supported)");

            Tracks = new MidiTrack[TracksCount];
            for (var i = 0; i < TracksCount; i++)
                Tracks[i] = ParseTrack(i, data, ref position);

            return new MidiFile(Format, TicksPerQuarterNote, Tracks) as T;
        }

        private static MidiTrack ParseTrack(int index, byte[] data, ref int position)
        {
            if (Reader.ReadString(data, ref position, 4) != "MTrk")
                throw new FormatException("Invalid track header (expected MTrk)");

            var trackLength = Reader.Read32(data, ref position);
            var trackEnd = position + trackLength;

            var track = new MidiTrack { Index = index };
            var time = 0;
            byte status = 0;

            while (position < trackEnd)
            {
                time += Reader.ReadVarInt(data, ref position);
                if (position >= data.Length) break;

                var peek = data[position];

                // Якщо MSB встановлено — це статус-байт; інакше — running status
                if ((peek & 0x80) != 0)
                {
                    status = peek;
                    position++;
                }

                if ((status & 0xF0) != 0xF0)
                {
                    // ---- Channel Voice / Mode messages ----
                    var eventType = (byte)(status & 0xF0);
                    var channel = (byte)(status & 0x0F); // 0..15

                    if (position >= trackEnd) break;
                    var data1 = data[position++];

                    byte data2 = 0;
                    // Події 0xC0 (Program Change) та 0xD0 (Channel Pressure) мають 1 data byte; інші — 2
                    if (eventType != 0xC0 && eventType != 0xD0)
                    {
                        if (position >= trackEnd) break;
                        data2 = data[position++];
                    }

                    // Convert NoteOn with velocity 0 -> NoteOff
                    if (eventType == (byte)MidiEventType.NoteOn && data2 == 0)
                        eventType = (byte)MidiEventType.NoteOff;

                    // FIX: Подаємо note у Arg1, velocity у Arg2, КАНАЛ кладемо у Arg3
                    // ПІДПРАВ CreateMidiEvent: очікує (time, type, arg1=note, arg2=vel, arg3=channel)
                    track.MidiEvents.Add(
                        MidiEventFactory.CreateMidiEvent(time, eventType, data1, data2, channel)
                    );
                }
                else
                {
                    // ---- System / Meta ----
                    if (status == 0xFF)
                    {
                        if (position >= trackEnd) break;
                        var metaType = Reader.Read8(data, ref position);

                        // Довжина meta завжди VLQ
                        var len = Reader.ReadVarInt(data, ref position);
                        if (position + len > trackEnd) len = Math.Max(0, trackEnd - position); // guard

                        if (metaType >= 0x01 && metaType <= 0x0F)
                        {
                            // Текстові meta-події
                            var textValue = Reader.ReadString(data, ref position, len);
                            var textEvent = new TextEvent { Time = time, Type = (byte)metaType, Value = textValue };
                            track.TextEvents.Add(textEvent);
                        }
                        else
                        {
                            // FIX: Розбір відомих meta з перевіркою довжини
                            switch (metaType)
                            {
                                case (byte)MetaEventType.Tempo:
                                    // 3 байти MPQN
                                    if (len == 3)
                                    {
                                        byte b1 = data[position];
                                        byte b2 = data[position + 1];
                                        byte b3 = data[position + 2];

                                        track.MidiEvents.Add(
                                            MidiEventFactory.CreateMetaEvent(time, metaType, b1, b2, b3)
                                        );
                                    }
                                    position += len;
                                    break;

                                case (byte)MetaEventType.TimeSignature:
                                    // 4 байти: nn, ddPow, cc, bb
                                    if (len == 4)
                                    {
                                        byte nn = data[position];
                                        byte ddPow = data[position + 1]; // ступінь 2, НЕ реальний знаменник
                                        byte cc = data[position + 2];
                                        byte bb = data[position + 3];

                                        // Кладемо nn, ddPow, cc у Arg1..Arg3 (bb за потреби можна додати окремо)
                                        track.MidiEvents.Add(
                                            MidiEventFactory.CreateMetaEvent(time, metaType, nn, ddPow, cc)
                                        );
                                    }
                                    position += len;
                                    break;

                                case (byte)MetaEventType.KeySignature:
                                    // 2 байти: sf, mi
                                    if (len == 2)
                                    {
                                        byte sf = data[position];     // -7..+7 (дваʼs complement у байті)
                                        byte mi = data[position + 1]; // 0=major, 1=minor
                                        track.MidiEvents.Add(
                                            MidiEventFactory.CreateMetaEvent(time, metaType, sf, mi, 0)
                                        );
                                    }
                                    position += len;
                                    break;

                                // End of Track
                                case 0x2F:
                                    // довжина має бути 0
                                    position += len; // якщо len != 0 — просто пропускаємо
                                    track.MidiEvents.Add(
                                        MidiEventFactory.CreateMetaEvent(time, 0x2F, 0, 0, 0)
                                    );
                                    // Специфікація: це остання подія треку — можна швидко дійти до кінця
                                    position = trackEnd;
                                    break;

                                default:
                                    // Інші meta — пропускаємо
                                    position += len;
                                    break;
                            }
                        }

                        // FIX: Meta/SysEx скасовують running status
                        status = 0;
                    }
                    else if (status == 0xF0 || status == 0xF7)
                    {
                        // SysEx event: len (VLQ) + len bytes
                        var len = Reader.ReadVarInt(data, ref position);
                        if (position + len > trackEnd) len = Math.Max(0, trackEnd - position);
                        position += len;

                        // FIX: SysEx скасовує running status
                        status = 0;
                    }
                    else
                    {
                        // Інші системні повідомлення (реал-тайм і т.д.) — 1 байт, рухаємось далі
                        // (реал-тайм можуть бути між будь-якими іншими подіями)
                        // Тут ми вже з'їли статус-байт, тож інкремент робити не треба
                        status = 0; // скасуємо надійно
                    }
                }
            }

            return track;
        }

        private static class Reader
        {
            public static int Read16(byte[] data, ref int i)
            {
                if (i + 2 > data.Length) throw new EndOfStreamException();
                return data[i++] << 8 | data[i++];
            }

            public static int Read32(byte[] data, ref int i)
            {
                if (i + 4 > data.Length) throw new EndOfStreamException();
                return (data[i++] << 24) | (data[i++] << 16) | (data[i++] << 8) | data[i++];
            }

            public static byte Read8(byte[] data, ref int i)
            {
                if (i + 1 > data.Length) throw new EndOfStreamException();
                return data[i++];
            }

            public static byte[] ReadAllBytesFromStream(Stream input)
            {
                var buffer = new byte[16 * 1024];
                using var ms = new MemoryStream();
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    ms.Write(buffer, 0, read);
                return ms.ToArray();
            }

            public static string ReadString(byte[] data, ref int i, int length)
            {
                if (i + length > data.Length) throw new EndOfStreamException();
                var result = Encoding.ASCII.GetString(data, i, length);
                i += length;
                return result;
            }

            public static int ReadVarInt(byte[] data, ref int i)
            {
                // Variable Length Quantity (до 4 байт)
                int result = 0;
                for (int count = 0; count < 4; count++)
                {
                    if (i >= data.Length) throw new EndOfStreamException();
                    byte b = data[i++];
                    result = (result << 7) | (b & 0x7F);
                    if ((b & 0x80) == 0) break;
                }
                return result;
            }
        }
    }
}
