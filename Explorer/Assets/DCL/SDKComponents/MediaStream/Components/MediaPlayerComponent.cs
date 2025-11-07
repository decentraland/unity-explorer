using DCL.ECSComponents;
using System.Threading;
using Utility;

namespace DCL.SDKComponents.MediaStream
{
    public struct MediaPlayerComponent
    {
        public const float DEFAULT_VOLUME = 1f;
        public const float DEFAULT_PLAYBACK_RATE = 1f;
        public const float DEFAULT_POSITION = 0f;

        public readonly MultiMediaPlayer MediaPlayer;
        public readonly bool IsFromContentServer;

        public MediaAddress MediaAddress;

        public VideoState State { get; private set; }
        public VideoState LastPropagatedState;
        public float LastPropagatedVideoTime;
        public double PreviousCurrentTimeChecked;
        public float LastStateChangeTime { get; private set; }

        public CancellationTokenSource Cts;
        public OpenMediaPromise OpenMediaPromise;

        public MediaPlayerComponent(MultiMediaPlayer mediaPlayer, bool isFromContentServer) : this()
        {
            MediaPlayer = mediaPlayer;
            IsFromContentServer = isFromContentServer;
        }

        public readonly bool IsPlaying => MediaPlayer.IsPlaying;
        public readonly float CurrentTime => MediaPlayer.CurrentTime;
        public readonly float Duration => MediaPlayer.Duration;

        public void SetState(VideoState newState)
        {
            if (State == newState) return;
            State = newState;
            LastStateChangeTime = UnityEngine.Time.realtimeSinceStartup;
        }

        public void Dispose()
        {
            MediaPlayer.Dispose(MediaAddress);
            Cts.SafeCancelAndDispose();
        }
    }
}
