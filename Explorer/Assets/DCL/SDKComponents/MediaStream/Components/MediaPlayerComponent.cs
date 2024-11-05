using DCL.ECSComponents;
using RenderHeads.Media.AVProVideo;
using System.Threading;
using Utility;

namespace DCL.SDKComponents.MediaStream
{
    public struct MediaPlayerComponent
    {
        public const float DEFAULT_VOLUME = 1f;
        public const float DEFAULT_PLAYBACK_RATE = 1f;
        public const float DEFAULT_POSITION = 0f;

        public MediaPlayer MediaPlayer;

        public string URL;
        public bool IsFromContentServer;
        public VideoState State { get; private set; }
        public VideoState LastPropagatedState;
        public double PreviousCurrentTimeChecked;
        public float LastStateChangeTime { get; private set; }

        public CancellationTokenSource Cts;
        public OpenMediaPromise OpenMediaPromise;

        public bool IsPlaying => MediaPlayer.Control.IsPlaying();
        public float CurrentTime => (float)MediaPlayer.Control.GetCurrentTime();
        public float Duration => (float)MediaPlayer.Info.GetDuration();

        public void SetState(VideoState newState)
        {
            if (State == newState) return;
            State = newState;
            LastStateChangeTime = UnityEngine.Time.realtimeSinceStartup;
        }

        public void Dispose()
        {
            MediaPlayer = null;
            Cts.SafeCancelAndDispose();
        }
    }
}
