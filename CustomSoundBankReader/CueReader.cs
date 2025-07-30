using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Instrumentarria.CustomSoundBankReader
{
    /// <summary>Manages the playback of a sound or set of sounds.</summary>
    /// <remarks>
    /// <para>Cues are comprised of one or more sounds.</para>
    /// <para>Cues also define specific properties such as pitch or volume.</para>
    /// <para>Cues are referenced through SoundBank objects.</para>
    /// </remarks>
    public class CueReader : IDisposable
    {
        private readonly string _name;
        private readonly XactSoundReader[] _sounds;
        private readonly float[] _probs;
        public int trackIndex = -1;
        private XactSoundReader _curSound;
        private float _volume = 1.0f;

        /// <summary>Gets the friendly name of the cue.</summary>
        /// <remarks>The friendly name is a value set from the designer.</remarks>
        public string Name
        {
            get { return _name; }
        }

        internal CueReader(string cuename, XactSoundReader sound)
        {
            //_engine = engine;
            _name = cuename;
            _sounds = new XactSoundReader[1];
            _sounds[0] = sound;
            _probs = new float[1];
            _probs[0] = 1.0f;
            trackIndex = _sounds[0].trackIndex;
        }

        internal CueReader(string cuename, XactSoundReader[] sounds, float[] probs)
        {
            //_engine = engine;
            _name = cuename;
            _sounds = sounds;
            _probs = probs;
        }
        #region IDisposable implementation
        /// <summary>Immediately releases any unmanaged resources used by this object.</summary>
        public void Dispose()
        {
        }
        #endregion
    }
}

