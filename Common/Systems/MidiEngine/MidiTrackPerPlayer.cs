using Instrumentarria.Common.Players;
using Instrumentarria.Common.Systems.AssetsManagers;
using Instrumentarria.Common.Systems.InstrumentsSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.Audio;

namespace Instrumentarria.Common.Systems.MidiEngine
{
    public class MidiTrackPerPlayer : IDisposable
    {
        private static List<MidiAudioTrack> _fadingTracks = new();

        private MidiAudioTrack _midiAudioTrack;

        private InstrumentarriaPlayer _player;

        public MidiTrackPerPlayer(InstrumentarriaPlayer player)
        {
            _player = player;
            _midiAudioTrack = CreateMidiTrack();
            _midiAudioTrack?.Play();
        }

        public void Update()
        {
            if (_midiAudioTrack == null)
            {
                return;
            }

            if (!_midiAudioTrack.isValid())
            {
                FadeCurrent();

                _midiAudioTrack = CreateMidiTrack();
                if (_midiAudioTrack == null)
                {
                    Log.Warn("Failed to create new MIDI track during BG sync update.");
                    return;
                }
                _midiAudioTrack.Play();
            }

            _midiAudioTrack.Update();
        }

        private void FadeCurrent()
        {
            if (_midiAudioTrack == null)
            {
                return;
            }
            _midiAudioTrack.StopWithFadeOut();
            _fadingTracks.Add(_midiAudioTrack);
            _midiAudioTrack = null;
        }

        public MidiAudioTrack CreateMidiTrack()
        {
            if (!MidiTracksController.MusicToMidiMap.TryGetValue(Main.curMusic, out var midiKey))
            {
                Log.Warn($"No MIDI mapping found for music slot {Main.curMusic}");
                return default;
            }

            var midiFile = MidiAssets.GetMidi(midiKey);
            if (midiFile == null)
            {
                Log.Warn($"MIDI file '{midiKey}' not found for music slot {Main.curMusic}");
                return default;
            }

            if (Main.audioSystem is not LegacyAudioSystem legacyAudioSystem)
            {
                Log.Warn($"Cannot sync MIDI - audio system is not LegacyAudioSystem");
                return default;
            }

            return new MidiAudioTrack(
                midiFile,
                _player.ActiveInstrument.SFInstrument,
                Main.curMusic
            );
        }

        public static void UpdateFadingTracks()
        {
            for (int i = _fadingTracks.Count - 1; i >= 0; i--)
            {
                var track = _fadingTracks[i];

                track.Update();
                if (track.IsDisposed)
                {
                    _fadingTracks.Remove(track);
                }
            }
        }

        public void Dispose()
        {
            FadeCurrent();
            _midiAudioTrack = null;
        }

        public void Pause()
        {
            _midiAudioTrack?.Pause();
        }

        public void Resume()
        {
            _midiAudioTrack?.Resume();
        }
    }
}
