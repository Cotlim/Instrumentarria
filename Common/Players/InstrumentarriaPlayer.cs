using Instrumentarria.Common.Systems.AssetsManagers;
using Instrumentarria.Common.Systems.InstrumentsSystem;
using Instrumentarria.Common.Systems.MidiEngine;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace Instrumentarria.Common.Players
{
    public class InstrumentarriaPlayer : ModPlayer
    {
        public ITInstrument ActiveInstrument { get; private set; }

        private MidiAudioTrack _midiAudioTrack;

        public bool IsActive => ActiveInstrument != null;

        public void ActivateInstrument(ITInstrument instrument)
        {
            ActiveInstrument = instrument;
        }

        public void DeactivateInstrument()
        {
            ActiveInstrument = null;
        }

        public void UpdateMidiTrack()
        {
            if (!IsActive)
            {
                if (_midiAudioTrack != null)
                {
                    FadeCurrent();
                }
                return;
            }

            if(_midiAudioTrack == null)
            {
                _midiAudioTrack = CreateMidiTrack();
                if (_midiAudioTrack == null)
                {
                    Log.Debug("Failed to create new MIDI track during BG sync update.");
                    return;
                }
                _midiAudioTrack.Play();
                return;
            }

            if (!_midiAudioTrack.IsValid())
            {
                FadeCurrent();

                _midiAudioTrack = CreateMidiTrack();
                if (_midiAudioTrack == null)
                {
                    Log.Debug("Failed to create new MIDI track during BG sync update.");
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
            ModContent.GetInstance<MidiTracksController>().AddFadingTrack(_midiAudioTrack);
            _midiAudioTrack = null;
        }

        public MidiAudioTrack CreateMidiTrack()
        {
            if (!MidiTracksController.MusicToMidiMap.TryGetValue(Main.curMusic, out var midiKey))
            {
                Log.Debug($"No MIDI mapping found for music slot {Main.curMusic}");
                return default;
            }

            var midiFile = MidiAssets.GetMidi(midiKey);
            if (midiFile == null)
            {
                Log.Debug($"MIDI file '{midiKey}' not found for music slot {Main.curMusic}");
                return default;
            }

            if (Main.audioSystem is not LegacyAudioSystem legacyAudioSystem)
            {
                Log.Debug($"Cannot sync MIDI - audio system is not LegacyAudioSystem");
                return default;
            }

            return new MidiAudioTrack(
                midiFile,
                ActiveInstrument.SFInstrument,
                Main.curMusic
            );
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
