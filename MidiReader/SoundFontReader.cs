using MeltySynth;
using ReLogic.Content;
using ReLogic.Content.Readers;
using System.IO;

namespace Instrumentarria.MidiReader
{
    /// <summary>
    /// Asset reader for SoundFont (.sf2) files using MeltySynth library.
    /// </summary>
    internal class SoundFontReader : IAssetReader
    {
        public T FromStream<T>(Stream stream) where T : class
        {
            if (typeof(T) != typeof(SoundFont))
                throw AssetLoadException.FromInvalidReader<SoundFontReader, T>();

            // MeltySynth requires seekable stream
            // If stream is not seekable, load into MemoryStream
            if (!stream.CanSeek)
            {
                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                memoryStream.Position = 0;
                return new SoundFont(memoryStream) as T;
            }

            return new SoundFont(stream) as T;
        }
    }
}
