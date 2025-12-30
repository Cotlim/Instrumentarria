using Instrumentarria.Common.Systems.AssetsManagers;
using MeltySynth;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Instrumentarria.Common.Systems.MidiEngine
{
    /// <summary>
    /// System that synchronizes MIDI playback with vanilla background music tracks.
    /// Maps specific music slots to MIDI files for synchronized playback.
    /// </summary>
    public class BackgroundMidiSync : ModSystem
    {
        // Mapping: MusicSlot -> MIDI file key
        private readonly Dictionary<int, string> _musicToMidiMap = new();
        
        // Currently active track
        private MidiAudioTrack _currentTrack;
        private int _currentMusicSlot = -1;
        private SoundFont _soundFont;
        
        // Sync state
        private bool _isActive = false;

        public bool IsActive => _isActive;
        public bool IsPlaying => _currentTrack?.IsPlaying ?? false;
        public double CurrentTime => _currentTrack?.CurrentTime ?? 0.0;

        public override void Load()
        {
            _soundFont = SoundFontAssets.DefaultSoundFont;
            
            if (_soundFont == null)
            {
                Mod.Logger.Warn("SoundFont not loaded for BackgroundMidiSync!");
            }

            // Register music->MIDI mappings
            RegisterMidiMappings();
        }

        public override void Unload()
        {
            _currentTrack?.Dispose();
            _currentTrack = null;
            _musicToMidiMap.Clear();
            _soundFont = null;
        }

        /// <summary>
        /// Registers which MIDI files should play for which music tracks.
        /// </summary>
        private void RegisterMidiMappings()
        {
            // For now, hardcode Day theme -> Test.mid
            // Later this can be data-driven from config/json
            _musicToMidiMap[MusicID.OverworldDay] = "Midi_1";
            
            // TODO: Add more mappings as you create MIDI files
            // _musicToMidiMap[MusicID.Night] = "NightTheme";
            // _musicToMidiMap[MusicID.Underground] = "CaveAmbient";
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (!_isActive || _soundFont == null)
            {
                // Not active, stop any playing track
                if (_currentTrack?.IsPlaying ?? false)
                {
                    _currentTrack.Stop(AudioStopOptions.Immediate);
                    _currentTrack.Dispose();
                    _currentTrack = null;
                }
                return;
            }

            var musicDetector = ModContent.GetInstance<MusicDetector>();
            int currentMusicSlot = musicDetector.CurrentMusicSlot;

            // Music changed or stopped
            if (currentMusicSlot != _currentMusicSlot)
            {
                StopCurrentTrack();
                _currentMusicSlot = currentMusicSlot;

                // Try to start new track if mapping exists
                if (_musicToMidiMap.TryGetValue(currentMusicSlot, out string midiKey))
                {
                    StartSyncedTrack(midiKey, currentMusicSlot);
                }
            }

            // Update current track
            _currentTrack?.Update();
        }

        /// <summary>
        /// Starts playing a MIDI track synchronized with the current background music position.
        /// </summary>
        private void StartSyncedTrack(string midiKey, int musicSlot)
        {
            // Load MIDI file
            var midiFile = MidiAssets.GetMidi(midiKey);
            if (midiFile == null)
            {
                Mod.Logger.Warn($"MIDI file '{midiKey}' not found for music slot {musicSlot}");
                return;
            }

            // Get the audio track to sync with
            if (Main.audioSystem is not LegacyAudioSystem legacyAudioSystem)
            {
                Mod.Logger.Warn($"Cannot sync MIDI - audio system is not LegacyAudioSystem");
                return;
            }

            if (musicSlot < 0 || musicSlot >= legacyAudioSystem.AudioTracks.Length)
            {
                Mod.Logger.Warn($"Invalid music slot {musicSlot}");
                return;
            }

            var targetTrack = legacyAudioSystem.AudioTracks[musicSlot];
            if (targetTrack == null)
            {
                Mod.Logger.Warn($"No track found at music slot {musicSlot}");
                return;
            }

            Log.Debug($"Creating MIDI track '{midiKey}' for music slot {musicSlot}");

            // Create new track
            _currentTrack = new MidiAudioTrack(midiFile, new(_soundFont, 0, 0, ""), MidiPlayer.DEFAULT_SAMPLE_RATE);
            
            // Set sync target to continuously sync with background music
            _currentTrack.SetSyncTarget(targetTrack);

            // Start playback - it will automatically sync to current position
            _currentTrack.Play();

            Log.Debug($"Started synced MIDI '{midiKey}' (Music slot: {musicSlot})");
        }

        /// <summary>
        /// Stops the currently playing track.
        /// </summary>
        private void StopCurrentTrack()
        {
            if (_currentTrack != null)
            {
                _currentTrack.Stop(AudioStopOptions.Immediate);
                _currentTrack.Dispose();
                _currentTrack = null;
            }
            _currentMusicSlot = -1;
        }

        /// <summary>
        /// Activates MIDI synchronization (call when player equips synced item).
        /// </summary>
        public void Activate()
        {
            _isActive = true;
        }

        /// <summary>
        /// Deactivates MIDI synchronization (call when player unequips synced item).
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
            StopCurrentTrack();
        }

        /// <summary>
        /// Checks if a MIDI mapping exists for the given music slot.
        /// </summary>
        public bool HasMappingFor(int musicSlot)
        {
            return _musicToMidiMap.ContainsKey(musicSlot);
        }
    }
}
