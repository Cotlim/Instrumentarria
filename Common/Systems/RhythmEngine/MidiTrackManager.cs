using Instrumentarria.MidiReader;
using MeltySynth;
using System.Collections.Generic;
using Terraria.ModLoader;

namespace Instrumentarria.Common.Systems.RhythmEngine
{
	/// <summary>
	/// Manages multiple MIDI tracks and their associations with background music.
	/// Follows Single Responsibility Principle - only manages MIDI track registry.
	/// </summary>
	public class MidiTrackManager : ModSystem
	{
		/// <summary>
		/// Represents a registered MIDI track with its associated music slot.
		/// </summary>
		public class MidiTrackRegistration
		{
			public string TrackId { get; set; }
			public MidiFile MidiFile { get; set; }
			public int MusicSlot { get; set; }
			public string DisplayName { get; set; }

			public MidiTrackRegistration(string trackId, MidiFile midiFile, int musicSlot, string displayName = null)
			{
				TrackId = trackId;
				MidiFile = midiFile;
				MusicSlot = musicSlot;
				DisplayName = displayName ?? trackId;
			}
		}

		// Registry of all available MIDI tracks
		private readonly Dictionary<string, MidiTrackRegistration> _trackRegistry = new();

		// Quick lookup: music slot -> track ID
		private readonly Dictionary<int, string> _musicSlotToTrack = new();

		public override void Unload()
		{
			_trackRegistry.Clear();
			_musicSlotToTrack.Clear();
		}

		/// <summary>
		/// Registers a MIDI track with a music slot for synchronized playback.
		/// </summary>
		/// <param name="trackId">Unique identifier for this track</param>
		/// <param name="midiFile">The MIDI file</param>
		/// <param name="musicSlot">The music slot ID (from MusicLoader.GetMusicSlot)</param>
		/// <param name="displayName">Optional display name for the track</param>
		public void RegisterTrack(string trackId, MidiFile midiFile, int musicSlot, string displayName = null)
		{
			if (_trackRegistry.ContainsKey(trackId))
			{
                Log.Warn($"Track '{trackId}' already registered, overwriting.");
				_trackRegistry.Remove(trackId);
			}

			if (_musicSlotToTrack.ContainsKey(musicSlot))
			{
				Log.Warn($"Music slot {musicSlot} already assigned to track '{_musicSlotToTrack[musicSlot]}', overwriting.");
				_musicSlotToTrack.Remove(musicSlot);
			}

			var registration = new MidiTrackRegistration(trackId, midiFile, musicSlot, displayName);
			_trackRegistry[trackId] = registration;
			_musicSlotToTrack[musicSlot] = trackId;

			Log.Info($"Registered MIDI track '{trackId}' with music slot {musicSlot}");
		}

		/// <summary>
		/// Unregisters a MIDI track.
		/// </summary>
		public void UnregisterTrack(string trackId)
		{
			if (_trackRegistry.TryGetValue(trackId, out var registration))
			{
				_musicSlotToTrack.Remove(registration.MusicSlot);
				_trackRegistry.Remove(trackId);
				Log.Info($"Unregistered MIDI track '{trackId}'");
			}
		}

		/// <summary>
		/// Gets a registered track by its ID.
		/// </summary>
		public MidiTrackRegistration GetTrack(string trackId)
		{
			_trackRegistry.TryGetValue(trackId, out var registration);
			return registration;
		}

		/// <summary>
		/// Gets a registered track by its music slot.
		/// </summary>
		public MidiTrackRegistration GetTrackByMusicSlot(int musicSlot)
		{
			if (_musicSlotToTrack.TryGetValue(musicSlot, out var trackId))
			{
				return GetTrack(trackId);
			}
			return null;
		}

		/// <summary>
		/// Checks if a track is registered.
		/// </summary>
		public bool IsTrackRegistered(string trackId)
		{
			return _trackRegistry.ContainsKey(trackId);
		}

		/// <summary>
		/// Gets all registered track IDs.
		/// </summary>
		public IEnumerable<string> GetAllTrackIds()
		{
			return _trackRegistry.Keys;
		}

		/// <summary>
		/// Gets all registered tracks.
		/// </summary>
		public IEnumerable<MidiTrackRegistration> GetAllTracks()
		{
			return _trackRegistry.Values;
		}

		/// <summary>
		/// Gets the count of registered tracks.
		/// </summary>
		public int TrackCount => _trackRegistry.Count;
	}
}
