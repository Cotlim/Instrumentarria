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
	/// Central controller for managing MIDI playback synchronized with background music.
	/// Tracks active players, manages MIDI-to-music mappings, and coordinates track lifecycle.
	/// </summary>
	public class MidiTracksController : ModSystem
	{
		private Dictionary<InstrumentarriaPlayer, MidiTrackPerPlayer> _activePlayersToMidi = new();

		private static readonly Dictionary<int, string> _musicToMidiMap = new();

		public static IReadOnlyDictionary<int, string> MusicToMidiMap => _musicToMidiMap;

		public override void Load()
		{
			On_LegacyAudioSystem.PauseAll += (orig, self) =>
            {
                orig(self);
                Pause();
            };

			On_LegacyAudioSystem.ResumeAll += (orig, self) =>
            {
                orig(self);
                Resume();
            };

			On_LegacyAudioSystem.Update += (orig, self) =>
			{
				orig(self);
				Update();
			};

			_musicToMidiMap[MusicID.OverworldDay] = "Midi_1";
			// TODO: Add more mappings
			// _musicToMidiMap[MusicID.Night] = "NightTheme";
			// _musicToMidiMap[MusicID.Underground] = "CaveAmbient";

		}

        private void Pause()
        {
            foreach ((var player, var midiSynchronizer) in _activePlayersToMidi)
            {
                midiSynchronizer.Pause();
            }
        }

        private void Resume()
        {
            foreach ((var player, var midiSynchronizer) in _activePlayersToMidi)
            {
                midiSynchronizer.Resume();
            }
        }

        public void Update()
		{
			//TODO check if players in range
			foreach ((var itplayer, var midiSynchronizer) in _activePlayersToMidi)
			{
				if (!Main.instance.IsActive || !itplayer.Player.active || itplayer.Player.dead)
				{
					midiSynchronizer.Pause();
					continue;
				}
				else
				{
					midiSynchronizer.Resume();
				}
				midiSynchronizer.Update();
			}
			MidiTrackPerPlayer.UpdateFadingTracks();

		}

		public void AddActivePlayer(InstrumentarriaPlayer player)
		{
			var instrumentInfo = player.ActiveInstrument;
			if (instrumentInfo == null)
			{
				Log.Warn($"Player {player.Player.name} is not using an instrument");
				return;
			}

			var midiSynchronizer = new MidiTrackPerPlayer(player);
			_activePlayersToMidi.Add(player, midiSynchronizer);
		}

		public void AddMainPlayer()
		{
			var player = Main.LocalPlayer.GetModPlayer<InstrumentarriaPlayer>();
			AddActivePlayer(player);
		}

		public void RemoveActivePlayer(InstrumentarriaPlayer player)
		{
			if (_activePlayersToMidi.TryGetValue(player, out var midiSynchronizer))
			{
				midiSynchronizer.Dispose();
				_activePlayersToMidi.Remove(player);
			}
		}

		public void RemoveMainPlayer()
		{
			var player = Main.LocalPlayer.GetModPlayer<InstrumentarriaPlayer>();
			RemoveActivePlayer(player);
		}

		public bool IsActive(InstrumentarriaPlayer instPlayer)
		{
			return _activePlayersToMidi.ContainsKey(instPlayer);
		}
	}
}
