using Instrumentarria.Common.Systems.AssetsManagers;
using Instrumentarria.Common.Systems.RhythmEngine;
using Terraria.ID;
using Terraria.ModLoader;

namespace Instrumentarria.Common.Systems
{
	/// <summary>
	/// Example system that registers MIDI tracks with background music on mod load.
	/// This demonstrates how to set up multiple MIDI tracks that sync with different music.
	/// </summary>
	public class ExampleMidiTrackRegistration : ModSystem
	{
		public override void PostSetupContent()
		{
			// Only register on client
			if (Terraria.Main.dedServ)
				return;

			var trackManager = ModContent.GetInstance<MidiTrackManager>();

			// Example 1: Register Test MIDI with a music slot
			if (MidiAssets.TestMidi?.IsLoaded == true)
			{
				// Option A: Use an existing music track
				// int musicSlot = MusicLoader.GetMusicSlot(Mod, "Music/YourMusicTrack");

				// Option B: Use a placeholder slot (for testing)
				int musicSlot = MusicID.TownDay;

				trackManager.RegisterTrack(
					trackId: "test_midi",
					midiFile: MidiAssets.TestMidi.Value,
					musicSlot: musicSlot,
					displayName: "Test MIDI"
				);

                Log.Info("Registered 'test_midi' track");
			}

			// Example 2: Register multiple tracks
			// You would typically load different MIDI files and assign them to different music slots
			/*
			if (MidiAssets.BossBattleMidi?.IsLoaded == true)
			{
				int bossMusicSlot = MusicLoader.GetMusicSlot(Mod, "Music/BossBattle");
				trackManager.RegisterTrack(
					trackId: "boss_battle",
					midiFile: MidiAssets.BossBattleMidi.Value,
					musicSlot: bossMusicSlot,
					displayName: "Epic Boss Battle"
				);
			}

			if (MidiAssets.UndergroundMidi?.IsLoaded == true)
			{
				int undergroundMusicSlot = MusicLoader.GetMusicSlot(Mod, "Music/Underground");
				trackManager.RegisterTrack(
					trackId: "underground_theme",
					midiFile: MidiAssets.UndergroundMidi.Value,
					musicSlot: undergroundMusicSlot,
					displayName: "Underground Theme"
				);
			}

			if (MidiAssets.TownMidi?.IsLoaded == true)
			{
				int townMusicSlot = MusicLoader.GetMusicSlot(Mod, "Music/TownDay");
				trackManager.RegisterTrack(
					trackId: "town_day",
					midiFile: MidiAssets.TownMidi.Value,
					musicSlot: townMusicSlot,
					displayName: "Peaceful Town Day"
				);
			}
			*/

			Log.Info($"Registered {trackManager.TrackCount} MIDI track(s)");
		}
	}
}
