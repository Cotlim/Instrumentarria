using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Instrumentarria.Common.Systems.RhythmEngine
{
	public class MusicDetector : ModSystem
	{
		private int _currentMusicSlot = -1;
		private int _previousMusicSlot = -1;
		private int _musicChangedCounter = 0;

		public int CurrentMusicSlot => _currentMusicSlot;
		public int PreviousMusicSlot => _previousMusicSlot;
		public bool MusicJustChanged => _musicChangedCounter == 1;

		public override void PostUpdateEverything()
		{
			int newMusicSlot = Main.curMusic;

			if (newMusicSlot != _currentMusicSlot)
			{
				_previousMusicSlot = _currentMusicSlot;
				_currentMusicSlot = newMusicSlot;
				_musicChangedCounter = 1;

				string musicName = GetMusicName(_currentMusicSlot);
				Mod.Logger.Debug($"Music changed: {_previousMusicSlot} -> {_currentMusicSlot} ({musicName})");
			}
			else if (_musicChangedCounter > 0)
			{
				_musicChangedCounter++;
				if (_musicChangedCounter > 10)
				{
					_musicChangedCounter = 0;
				}
			}
		}

		public string GetMusicName(int musicSlot)
		{
			return musicSlot switch
			{
				MusicID.OverworldDay => "Overworld Day",
				MusicID.Title => "Title Screen",
				MusicID.Desert => "Desert",
				MusicID.Jungle => "Jungle",
				MusicID.Corruption => "Corruption",
				MusicID.Crimson => "Crimson",
				MusicID.Ocean => "Ocean",
				MusicID.Snow => "Snow",
				MusicID.Mushrooms => "Mushroom Biome",
				MusicID.Dungeon => "Dungeon",
				MusicID.Temple => "Lihzahrd Temple",
				MusicID.Graveyard => "Graveyard",
				MusicID.Night => "Night",
				MusicID.Underground => "Underground",
				MusicID.UndergroundCorruption => "Underground Corruption",
				MusicID.UndergroundCrimson => "Underground Crimson",
				MusicID.UndergroundHallow => "Underground Hallow",
				MusicID.UndergroundDesert => "Underground Desert",
				MusicID.Hell => "Underworld",
				MusicID.Space => "Space",
				MusicID.Rain => "Rain",
				MusicID.Eclipse => "Solar Eclipse",
				MusicID.PirateInvasion => "Pirate Invasion",
				MusicID.MartianMadness => "Martian Madness",
				MusicID.PumpkinMoon => "Pumpkin Moon",
				MusicID.FrostMoon => "Frost Moon",
				MusicID.Sandstorm => "Sandstorm",
				MusicID.OldOnesArmy => "Old One's Army",
				MusicID.Boss1 => "Boss 1",
				MusicID.Boss2 => "Boss 2",
				MusicID.Boss3 => "Boss 3",
				MusicID.Boss4 => "Boss 4",
				MusicID.Boss5 => "Boss 5",
				MusicID.Plantera => "Plantera",
				MusicID.DukeFishron => "Duke Fishron",
				MusicID.LunarBoss => "Moon Lord",
				MusicID.QueenSlime => "Queen Slime",
				MusicID.Deerclops => "Deerclops",
				0 => "No Music",
				-1 => "Not Set",
				_ => $"Music ({musicSlot})"
			};
		}

		public bool IsPlayingAnyOf(params int[] musicSlots)
		{
			foreach (int slot in musicSlots)
			{
				if (_currentMusicSlot == slot)
					return true;
			}
			return false;
		}
	}
}
