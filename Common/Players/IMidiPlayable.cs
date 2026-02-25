namespace Instrumentarria.Common.Players
{
    /// <summary>
    /// Interface for items and tiles that can play MIDI music.
    /// </summary>
    public interface IMidiPlayable
    {
        public void UpdateMidiTrack();

        public void Pause();
        public void Resume();
    }
}