using Instrumentarria.Common.Players;
using Instrumentarria.Common.Systems.AssetsManagers;
using Instrumentarria.Common.Systems.InstrumentsSystem;
using MeltySynth;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Instrumentarria.Common.Systems.MidiEngine
{
    /// <summary>
    /// new version of MidiController
    /// </summary>
    public class MidiTracksController : ModSystem
    {
        private Dictionary<InstrumentarriaPlayer, MidiBGSync> _activePlayersToMidi = new();

        private static readonly Dictionary<int, string> _musicToMidiMap = new();

        public static IReadOnlyDictionary<int, string> MusicToMidiMap => _musicToMidiMap;

        public override void Load()
        {
            _musicToMidiMap[MusicID.OverworldDay] = "Midi_1";

            // TODO: Add more mappings
            // _musicToMidiMap[MusicID.Night] = "NightTheme";
            // _musicToMidiMap[MusicID.Underground] = "CaveAmbient";

        }

        public override void UpdateUI(GameTime gameTime)
        {
            //TODO check if players in range
            foreach ((var player, var midiBGSync) in _activePlayersToMidi)
            {
                midiBGSync.Update();
            }
            MidiBGSync.UpdateFadingTracks();

        }

        public void AddActivePlayer(InstrumentarriaPlayer player)
        {
            var instrumentInfo = player.ActiveInstrument;
            if (instrumentInfo == null)
            {
                Log.Warn($"Player {player.Player.name} is not using an instrument");
                return;
            }

            var midiTrack = new MidiBGSync(player);
            _activePlayersToMidi.Add(player, midiTrack);
        }

        public void AddMainPlayer()
        {
            var player = Main.LocalPlayer.GetModPlayer<InstrumentarriaPlayer>();
            AddActivePlayer(player);
        }

        public void RemoveActivePlayer(InstrumentarriaPlayer player)
        {
            if (_activePlayersToMidi.TryGetValue(player, out var midiBGSync))
            {
                midiBGSync.Dispose();
                _activePlayersToMidi.Remove(player);
            }
        }

        public void RemoveMainPlayer()
        {
            var player = Main.LocalPlayer.GetModPlayer<InstrumentarriaPlayer>();
            RemoveActivePlayer(player);
        }

        public MidiAudioTrack CreateMidiTrack(ITInstrument instrumentInfo)
        {
            var midiKey = _musicToMidiMap[Main.curMusic];
            var midiFile = MidiAssets.GetMidi(midiKey);
            if (midiFile == null)
            {
                Mod.Logger.Warn($"MIDI file '{midiKey}' not found for music slot {Main.curMusic}");
                return default;
            }

            if (Main.audioSystem is not LegacyAudioSystem legacyAudioSystem)
            {
                Mod.Logger.Warn($"Cannot sync MIDI - audio system is not LegacyAudioSystem");
                return default;
            }

            return new MidiAudioTrack(
                midiFile,
                instrumentInfo.SoundFontAsset,
                Main.curMusic
            );
        }

        public bool IsActive(InstrumentarriaPlayer instPlayer)
        {
            return _activePlayersToMidi.ContainsKey(instPlayer);
        }
    }
}
