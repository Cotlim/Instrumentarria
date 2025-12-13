using System;

namespace Instrumentarria.Common.Systems.RhythmEngine
{
    /// <summary>
    /// Допоміжний клас для конвертації аудіо форматів
    /// </summary>
    public static class AudioHelper
    {
        /// <summary>
        /// Розраховує частоту для MIDI ноти
        /// </summary>
        public static float MidiNoteToFrequency(byte midiNote)
        {
            // A4 (MIDI 69) = 440 Hz
            return 440f * MathF.Pow(2f, (midiNote - 69) / 12f);
        }
    }
}
