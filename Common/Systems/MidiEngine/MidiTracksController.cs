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
        private static readonly Dictionary<int, string> _musicToMidiMap = new();

        private static List<MidiAudioTrack> _fadingTracks = new();

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
            foreach (var player in Main.ActivePlayers)
            {
                player.GetModPlayer<InstrumentarriaPlayer>().Pause();
            }
        }

        private void Resume()
        {
            foreach (var player in Main.ActivePlayers)
            {
                player.GetModPlayer<InstrumentarriaPlayer>().Resume();
            }
        }

        public void Update()
        {
            //TODO check if players in range
            foreach (var player in Main.ActivePlayers)
            {
                var itplayer = player.GetModPlayer<InstrumentarriaPlayer>();
                if (!Main.instance.IsActive || player.dead)
                {
                    itplayer.Pause();
                    continue;
                }
                else
                {
                    itplayer.Resume();
                }
                itplayer.UpdateMidiTrack();
            }
            UpdateFadingTracks();

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

        public void AddFadingTrack(MidiAudioTrack track)
        {
            _fadingTracks.Add(track);
        }
    }
}
