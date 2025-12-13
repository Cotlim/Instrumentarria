using Instrumentarria.MidiReader;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Instrumentarria.Common.Systems.RhythmEngine.Streaming
{
    /// <summary>
    /// Провайдер MIDI подій з MidiFile з pre-calculated timing
    /// </summary>
    public class MidiFileEventProvider : IMidiEventProvider
    {
        private readonly List<ScheduledEvent> _scheduledEvents;
        private int _currentEventIndex;
        private double _totalDuration;
        private double _initialBPM;

        private class ScheduledEvent
        {
            public int Tick;
            public double TimeInSeconds;
            public MidiEvent Event;
            public int TrackIndex;
        }

        public MidiFileEventProvider(MidiFile midiFile)
        {
            if (midiFile == null)
                throw new ArgumentNullException(nameof(midiFile));

            _scheduledEvents = FlattenTracksWithTime(midiFile);
            _currentEventIndex = 0;

            // Обчислити загальну тривалість
            if (_scheduledEvents.Count > 0)
            {
                _totalDuration = _scheduledEvents.Last().TimeInSeconds;
            }
        }

        public IEnumerable<(double timeInSeconds, MidiEvent midiEvent)> GetEventsUntil(double currentTime)
        {
            var events = new List<(double, MidiEvent)>();

            while (_currentEventIndex < _scheduledEvents.Count)
            {
                var scheduled = _scheduledEvents[_currentEventIndex];

                if (scheduled.TimeInSeconds > currentTime)
                    break;

                events.Add((scheduled.TimeInSeconds, scheduled.Event));
                _currentEventIndex++;
            }

            return events;
        }

        public void Reset()
        {
            _currentEventIndex = 0;
        }

        public bool HasMoreEvents => _currentEventIndex < _scheduledEvents.Count;

        public double? TotalDuration => _totalDuration;

        public double InitialBPM => _initialBPM;
        
        /// <summary>
        /// Час першої NoteOn події (для пропуску початкової тиші)
        /// </summary>
        public double FirstNoteTime
        {
            get
            {
                var firstNote = _scheduledEvents.FirstOrDefault(e => e.Event is NoteOnEvent);
                return firstNote?.TimeInSeconds ?? 0.0;
            }
        }

        private List<ScheduledEvent> FlattenTracksWithTime(MidiFile midi)
        {
            var events = new List<ScheduledEvent>();

            // Зібрати всі події з усіх треків
            var allEvents = new List<(int tick, int trackIndex, MidiEvent evt)>();

            for (int trackIndex = 0; trackIndex < midi.Tracks.Length; trackIndex++)
            {
                var track = midi.Tracks[trackIndex];
                foreach (var evt in track.MidiEvents)
                {
                    allEvents.Add((evt.Time, trackIndex, evt));
                }
            }

            // Сортувати за тіком
            allEvents.Sort((a, b) => a.tick.CompareTo(b.tick));

            // Витягнути початковий BPM
            double currentBpm = 120.0; // За замовчуванням
            var firstTempoEvent = midi.Tracks.Length > 0
                ? midi.Tracks[0].MidiEvents.OfType<TempoChangeEvent>().FirstOrDefault()
                : null;

            if (firstTempoEvent != null)
            {
                currentBpm = firstTempoEvent.Bpm;
            }

            _initialBPM = currentBpm; // Зберегти початковий BPM

            int ticksPerQuarterNote = midi.TicksPerQuarterNote;

            // Обчислити час для кожної події, враховуючи зміни темпу
            double currentTime = 0.0;
            int lastTick = 0;

            foreach (var (tick, trackIndex, evt) in allEvents)
            {
                // Обчислити час від останнього тіку до поточного
                int deltaTicks = tick - lastTick;
                if (deltaTicks > 0)
                {
                    double secondsPerTick = 60.0 / (currentBpm * ticksPerQuarterNote);
                    currentTime += deltaTicks * secondsPerTick;
                }

                // Перевірити чи це TempoChangeEvent
                if (evt is TempoChangeEvent tempoChange)
                {
                    currentBpm = tempoChange.Bpm;
                }

                events.Add(new ScheduledEvent
                {
                    Tick = tick,
                    TimeInSeconds = currentTime,
                    Event = evt,
                    TrackIndex = trackIndex
                });

                lastTick = tick;
            }

            return events;
        }
    }
}
