using Instrumentarria.Common.Systems.AssetsManagers;
using Instrumentarria.Common.Systems.InstrumentsSystem;
using Instrumentarria.Common.Systems.MidiEngine;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace Instrumentarria.Common.Players
{
    public class InstrumentarriaPlayer : ModPlayer, IMidiPlayable
    {
        private const float MAX_HEARING_DISTANCE = 800f;
        private const float MIN_HEARING_DISTANCE = 200f;
        public ITInstrument ActiveInstrument { get; private set; }

        private MidiAudioTrack _midiAudioTrack;

        public bool IsActive => ActiveInstrument != null;

        public bool IsPaused => _midiAudioTrack?.IsPaused ?? false;
        public void ActivateInstrument(ITInstrument instrument)
        {
            ActiveInstrument = instrument;
        }

        public void DeactivateInstrument()
        {
            ActiveInstrument = null;
        }

        public override void UpdateDead()
        {
            DeactivateInstrument();
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

            if (_midiAudioTrack == null || !_midiAudioTrack.IsValid())
            {
                if (_midiAudioTrack != null)
                    FadeCurrent();

                _midiAudioTrack = CreateMidiTrack();
                if (_midiAudioTrack == null)
                {
                    Log.Debug("Failed to create new MIDI track during BG sync update.");
                    return;
                }
                _midiAudioTrack.Play();
            }

            float distanceToPlayer = Vector2.Distance(Main.LocalPlayer.Center, Player.Center);
            float volumeScale;
            if (distanceToPlayer < MIN_HEARING_DISTANCE)
            {
                volumeScale = 1f;
            }
            else if (distanceToPlayer < MAX_HEARING_DISTANCE)
            {
                volumeScale = 1f - (distanceToPlayer - MIN_HEARING_DISTANCE) / (MAX_HEARING_DISTANCE - MIN_HEARING_DISTANCE);
            }
            else
            {
                volumeScale = 0f;
            }

            _midiAudioTrack.MusicVolume = Main.musicVolume * volumeScale;
            _midiAudioTrack.Update();
        }
        private void FadeCurrent()
        {
            if (_midiAudioTrack == null)
            {
                return;
            }
            _midiAudioTrack.StopWithFadeOut();
            MidiTracksController.AddFadingTrack(_midiAudioTrack);
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
