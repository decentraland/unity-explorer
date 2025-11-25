using DCL.ECSComponents;
using RenderHeads.Media.AVProVideo;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.SDKComponents.MediaStream
{
    public struct MediaPlayerComponent
    {
        public const float DEFAULT_VOLUME = 1f;
        public const float DEFAULT_PLAYBACK_RATE = 1f;
        public const float DEFAULT_POSITION = 0f;
        private const float PLAY_CHECK_THRESHOLD = 0.5f;

        private float lastVideoTime;
        private float frozenTimestamp;
        private float lastPlayTimestamp;
        private bool isFrozen;

        public readonly MultiMediaPlayer MediaPlayer;
        public readonly bool IsFromContentServer;

        public MediaAddress MediaAddress;
        public VideoState State { get; private set; }
        public bool HasFailed { get; private set; }
        public VideoState LastPropagatedState;
        public float LastPropagatedVideoTime;
        public CancellationTokenSource? Cts;
        public OpenMediaPromise? OpenMediaPromise;

        public MediaPlayerComponent(MultiMediaPlayer mediaPlayer, bool isFromContentServer) : this()
        {
            MediaPlayer = mediaPlayer;
            IsFromContentServer = isFromContentServer;
            HasFailed = false;
            State = VideoState.VsNone;
            isFrozen = false;
        }

        public readonly bool IsPlaying => MediaPlayer.IsPlaying;
        public readonly float CurrentTime => MediaPlayer.CurrentTime;
        public readonly float Duration => MediaPlayer.Duration;

        public VideoState UpdateState()
        {
            var player = MediaPlayer;
            var state = VideoState.VsNone;
            var isNowFrozen = false;

            if (MediaAddress.IsEmpty)
                state = VideoState.VsNone;
            else if (player.IsFinished)
                state = VideoState.VsNone;
            else if (HasFailed || player.GetLastError() != ErrorCode.None)
                state = VideoState.VsError;
            else if (player.IsPaused)
                state = VideoState.VsPaused;
            else if (player.IsPlaying)
            {
                state = VideoState.VsPlaying;

                float timestamp = UnityEngine.Time.realtimeSinceStartup;

                // This threshold solves the case on which it is updated many times in a row in the same frame
                if (timestamp - lastPlayTimestamp > PLAY_CHECK_THRESHOLD)
                {
                    lastPlayTimestamp = timestamp;

                    bool wasFrozen = isFrozen;
                    isNowFrozen = Math.Abs(player.CurrentTime - lastVideoTime) < Mathf.Epsilon;

                    if (!isNowFrozen)
                    {
                        lastVideoTime = player.CurrentTime;
                        frozenTimestamp = timestamp;
                    }
                    else
                    {
                        if (isNowFrozen != wasFrozen)
                            frozenTimestamp = timestamp;

                        if (player.IsSeeking)
                            state = VideoState.VsSeeking;
                        else if (player.IsBuffering)
                            state = VideoState.VsBuffering;
                    }
                }
            }

            State = state;
            isFrozen = isNowFrozen;

            return State;
        }

        /// <summary>
        /// Frozen means that the state is playing but the player keeps the same play time.
        /// Most likely because it is seeking or buffering
        /// </summary>
        /// <param name="frozenElapsedTime">Amount of seconds that elapsed since the media player was detected as "frozen"</param>
        /// <returns></returns>
        public readonly bool IsFrozen(out float frozenElapsedTime)
        {
            if (isFrozen)
                frozenElapsedTime = UnityEngine.Time.realtimeSinceStartup - frozenTimestamp;
            else
                frozenElapsedTime = 0f;

            return isFrozen;
        }

        public void MarkAsFailed(bool failed) =>
            HasFailed = failed;

        public void Dispose()
        {
            State = VideoState.VsNone;
            HasFailed = false;
            isFrozen = false;
            MediaPlayer.Dispose(MediaAddress);
            Cts.SafeCancelAndDispose();
        }
    }
}
