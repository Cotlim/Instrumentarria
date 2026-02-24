using Microsoft.Xna.Framework.Audio;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace Instrumentarria.Common.Systems.MidiEngine
{
	/// <summary>
	/// Synchronizes MIDI audio tracks with target audio tracks by managing buffer counts and playback positions.
	/// </summary>
	internal class AudioTrackSynchronizer
	{
		private readonly IAudioTrack _syncTarget;
		private readonly int _syncID;
		private readonly AudioTrackPositionTracker _positionTracker;
        
        // Periodic re-synchronization
        private int _buffersSinceLastSync = 0;
        private const int RESYNC_INTERVAL = 10; // Re-sync every 10 buffers (~0.5s)
        
        // Tolerance for backwards seeking
        private const double SEEK_BACKWARDS_TOLERANCE = 0.05; // 50ms

	public AudioTrackSynchronizer(int syncID)
	{
		_syncID = syncID;
		_syncTarget = GetTrackFromID(_syncID);
		_positionTracker = ModContent.GetInstance<AudioTrackPositionTracker>();
	}

        private IAudioTrack GetTrackFromID(int syncTarget)
        {
            if (Main.audioSystem is not LegacyAudioSystem legacyAudioSystem)
            {
                Log.Warn($"Cannot sync MIDI - audio system is not LegacyAudioSystem");
                return default;
            }

            if (syncTarget < 0 || syncTarget >= legacyAudioSystem.AudioTracks.Length)
            {
                Log.Warn($"Cannot sync MIDI - invalid sync target ID: {syncTarget}");
                return default;
            }
            return legacyAudioSystem.AudioTracks[syncTarget];
        }

	/// <summary>
	/// Gets the current position of the sync target track.
	/// </summary>
	public double GetTargetPosition()
	{
		return _positionTracker?.GetTrackPosition(_syncTarget) ?? 0;
	}

        /// <summary>
        /// Gets the pending buffer count of the sync target.
        /// Returns -1 if target is not compatible.
        /// </summary>
        public int GetTargetBufferCount()
        {
            if (_syncTarget is ASoundEffectBasedAudioTrack syncAudioTrack)
            {
                return syncAudioTrack._soundEffectInstance.PendingBufferCount;
            }
            return -1;
        }

        /// <summary>
        /// Checks if the sync target is valid and compatible.
        /// </summary>
        public bool IsValid()
        {
            return _syncID == Main.curMusic;
        }

        /// <summary>
        /// Determines the buffer synchronization action needed.
        /// </summary>
        public BufferSyncAction GetSyncAction(int currentBufferCount)
        {
            int targetBufferCount = GetTargetBufferCount();
            
            if (targetBufferCount < 0)
                return BufferSyncAction.Skip;

            int bufferDifference = currentBufferCount - targetBufferCount;

            // Allow tolerance of +-3 buffers
            if (bufferDifference > 3)
                return BufferSyncAction.Skip;

            if (bufferDifference < -3)
                return BufferSyncAction.AddWithFill;

            return BufferSyncAction.AddNormal;
        }
        
        /// <summary>
        /// Gets the synchronized time. Returns current time if no sync needed.
        /// </summary>
        public double GetSynchronizedTime(double currentTime, double maxTime)
        {
            _buffersSinceLastSync++;
            
            if (_buffersSinceLastSync < RESYNC_INTERVAL)
                return currentTime;
                
            _buffersSinceLastSync = 0;
            
            double targetTime = GetTargetPosition();
            targetTime = Math.Clamp(targetTime, 0, maxTime);
            
            double timeDifference = targetTime - currentTime;
            
            // Ignore minor backwards jitter
            if (timeDifference > -SEEK_BACKWARDS_TOLERANCE && timeDifference < 0)
                return currentTime;
            
            return targetTime;
        }
    }

    /// <summary>
    /// Defines the action to take for buffer synchronization.
    /// </summary>
    internal enum BufferSyncAction
    {
        Skip,
        AddNormal,
        AddWithFill
    }
}
