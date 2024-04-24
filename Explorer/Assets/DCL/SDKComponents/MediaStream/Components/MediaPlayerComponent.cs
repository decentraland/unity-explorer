using DCL.ECSComponents;
using RenderHeads.Media.AVProVideo;
using System;
using System.Threading;

namespace DCL.SDKComponents.MediaStream
{
    public struct MediaPlayerComponent : IDisposable
    {
        public const float DEFAULT_VOLUME = 1f;
        public const float DEFAULT_PLAYBACK_RATE = 1f;
        public const float DEFAULT_POSITION = 0f;

        public MediaPlayer MediaPlayer;
        public string URL;
        public CancellationTokenSource Cts;
        public VideoState State;

        public bool IsPlaying => MediaPlayer.Control.IsPlaying();
        public float CurrentTime => (float)MediaPlayer.Control.GetCurrentTime();
        public float Duration => (float)MediaPlayer.Info.GetDuration();

        public void Dispose()
        {
            MediaPlayer = null;
            Cts.Dispose();
        }
    }
}
