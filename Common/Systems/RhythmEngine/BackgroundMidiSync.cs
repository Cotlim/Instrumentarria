using Instrumentarria.Common.Systems.AssetsManagers;
using MeltySynth;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Instrumentarria.Common.Systems.RhythmEngine
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

        public override void PostUpdateEverything()
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

            // Maintain volume
            if (_currentTrack?.IsPlaying ?? false)
            {
                // Volume control can be added here if needed
            }
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

            // Get track position system for continuous sync
            var trackPositionSystem = ModContent.GetInstance<MusicTrackPositionSystem>();
            double initialPosition = trackPositionSystem?.GetTrackPosition(musicSlot) ?? 0;

            Mod.Logger.Debug($"Creating MIDI track '{midiKey}' for music slot {musicSlot}, initial position: {initialPosition:F2}s");

            // Create new track
            _currentTrack = new MidiAudioTrack(midiFile, _soundFont, MidiPlayer.DEFAULT_SAMPLE_RATE);
            
            // Set external time provider to continuously sync with background music
            _currentTrack.SetExternalTimeProvider(() => 
            {
                return trackPositionSystem?.GetTrackPosition(musicSlot) ?? 0.0;
            });

            // Start playback - it will automatically sync to current position
            _currentTrack.Play();

            Mod.Logger.Debug($"Started synced MIDI '{midiKey}' at {initialPosition:F2}s (Music slot: {musicSlot})");
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
