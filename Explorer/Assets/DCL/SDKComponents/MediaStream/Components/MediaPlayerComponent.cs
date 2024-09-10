using DCL.ECSComponents;
using DCL.Utilities.Extensions;
using RenderHeads.Media.AVProVideo;
using System.Runtime.CompilerServices;
using System.Threading;
using Utility;

namespace DCL.SDKComponents.MediaStream.Components
{
    public struct MediaPlayerComponent
    {
        public const float DEFAULT_VOLUME = 1f;
        public const float DEFAULT_PLAYBACK_RATE = 1f;
        public const float DEFAULT_POSITION = 0f;

        public MediaPlayer MediaPlayer;
        public VideoState State { get; private set; }

        public CancellationTokenSource Cts;
        public OpenMediaPromise OpenMediaPromise;

        public string URL { get; private set; }

        public readonly bool IsPlaying => MediaPlayer!.Control!.IsPlaying();
        public readonly float CurrentTime => (float)MediaPlayer!.Control!.GetCurrentTime();
        public readonly float Duration => (float)MediaPlayer!.Info!.GetDuration();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateUrlAndState(string newUrl)
        {
            this.URL = newUrl;
            State = newUrl.IsValidUrl() ? VideoState.VsNone : VideoState.VsError;
        }

        public void UpdateState(VideoState newState)
        {
            State = newState;
        }

        public void CloseCurrentStreamWithError()
        {
            State = VideoState.VsError;
            MediaPlayer.CloseCurrentStream();
        }

        public void Dispose()
        {
            MediaPlayer = null!;
            Cts.SafeCancelAndDispose();
        }
    }
}
