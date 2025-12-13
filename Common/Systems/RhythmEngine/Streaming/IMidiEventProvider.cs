using Instrumentarria.MidiReader;
using System.Collections.Generic;

namespace Instrumentarria.Common.Systems.RhythmEngine.Streaming
{
    /// <summary>
    /// Інтерфейс для класів що можуть надавати MIDI події з timing інформацією
    /// </summary>
    public interface IMidiEventProvider
    {
        /// <summary>
        /// Отримує наступні MIDI події що мають відбутися до вказаного часу
        /// </summary>
        /// <param name="currentTime">Поточний час відтворення в секундах</param>
        /// <returns>Колекція MIDI подій для обробки</returns>
        IEnumerable<(double timeInSeconds, MidiEvent midiEvent)> GetEventsUntil(double currentTime);

        /// <summary>
        /// Скидає провайдер подій до початку
        /// </summary>
        void Reset();

        /// <summary>
        /// Перевіряє чи залишились події для відтворення
        /// </summary>
        bool HasMoreEvents { get; }

        /// <summary>
        /// Загальна тривалість у секундах (якщо відома)
        /// </summary>
        double? TotalDuration { get; }
    }
}
