// SPDX-License-Identifier: Apache-2.0

using System;
using UnityEngine.Events;

namespace UnitedAV
{
    [Serializable]
    public class MediaPlayerEvent : UnityEvent<MediaPlayer, MediaPlayerEvent.EventType, ErrorCode>
    {
        public enum EventType
        {
            MetaDataReady,
            ReadyToPlay,
            Started,
            FirstFrameReady,
            FinishedPlaying,
            Closing,
            Error,
            SubtitleChange,
            Stalled,
            Unstalled,
            ResolutionChanged,
            StartedSeeking,
            FinishedSeeking,
            StartedBuffering,
            FinishedBuffering,
            PropertiesChanged,
            PlaylistItemChanged,
            PlaylistFinished,
            TextTracksChanged,
            TextCueChanged,
            Paused,
            Unpaused,
        }

        // UnityEvent exposes no runtime listener count, so track it ourselves for HasListeners().
        private int _runtimeListenerCount;

        public new void AddListener(UnityAction<MediaPlayer, EventType, ErrorCode> call)
        {
            base.AddListener(call);
            _runtimeListenerCount++;
        }

        public new void RemoveListener(UnityAction<MediaPlayer, EventType, ErrorCode> call)
        {
            base.RemoveListener(call);
            if (_runtimeListenerCount > 0)
                _runtimeListenerCount--;
        }

        public new void RemoveAllListeners()
        {
            base.RemoveAllListeners();
            _runtimeListenerCount = 0;
        }

        /// <summary>True if any listener (runtime-added or editor-persistent) is attached.</summary>
        public bool HasListeners()
        {
            return _runtimeListenerCount > 0 || GetPersistentEventCount() > 0;
        }
    }
}
