using UnityEngine;
using UUAV;

namespace DCL.AvProSwitch
{
    // UUAV path: attaches UUAVPlayer and maps its state machine onto the
    // AVPro-shaped surface the switch exposes. Platform options are inert here
    // (audio always routes through Unity, video always uses the D3D11 path).
    public sealed class UuavBackend
    {
        private static readonly TimeRanges NOT_BUFFERED = new TimeRanges(0);
        private static readonly TimeRanges BUFFERED = new TimeRanges(1);

        private readonly UUAVPlayer player;

        private bool seekRequested;

        public UuavBackend(GameObject gameObject)
        {
            player = gameObject.AddComponent<UUAVPlayer>();
        }

        public AudioSource AudioSource => player.AudioSource;

        public bool MediaOpened =>
            player.State is not (UUAVState.Closed or UUAVState.Error or UUAVState.Unknown);

        // UUAVPlayer builds its control and texture surfaces in Awake, so they
        // exist for the component's whole lifetime.
        public bool HasControl => true;

        // false while Opening, when the native player rejects seeks
        public bool IsReady =>
            player.State is UUAVState.Ready or UUAVState.Playing or UUAVState.Paused or UUAVState.Ended;

        public float AudioVolume
        {
            get => AudioSource ? AudioSource.volume : 0f;

            set
            {
                if (AudioSource)
                {
                    AudioSource.volume = value;
                }
            }
        }

        // pathType is accepted for signature parity: only AbsolutePathOrURL is
        // ever passed, and UUAV takes the URL verbatim.
        public bool OpenMedia(MediaPathType pathType, string path, bool autoPlay)
        {
            player.OpenMedia(path);

            if (autoPlay)
            {
                player.Play();
            }

            return true;
        }

        public void CloseMedia() => player.CloseMedia();

        public void Play() => player.Play();

        public void Pause() => player.Pause();

        // Pauses and seeks back to the start, matching AVPro's Stop semantics.
        public void Stop()
        {
            player.Pause();
            player.Seek(0);
        }

        public void Seek(double time)
        {
            seekRequested = true;
            player.Seek(time);
        }

        public bool IsPlaying() => player.State == UUAVState.Playing;

        public bool IsPaused() => player.State == UUAVState.Paused;

        // UUAV has no distinct seeking state, so a requested seek stands until
        // the player settles into one of the states a seek resolves to.
        public bool IsSeeking()
        {
            if (seekRequested
                && player.State is UUAVState.Ready or UUAVState.Playing or UUAVState.Paused)
            {
                seekRequested = false;
            }

            return seekRequested;
        }

        public bool IsBuffering() => player.State == UUAVState.Opening;

        public bool IsFinished() => player.State == UUAVState.Ended;

        public bool IsLooping() => player.Looping;

        public void SetLooping(bool looping) => player.Looping = looping;

        public double GetCurrentTime() => player.CurrentTime;

        public float GetPlaybackRate() => (float)player.PlaybackRate;

        public void SetPlaybackRate(float rate) => player.PlaybackRate = rate;

        public ErrorCode GetLastError() =>
            player.LastError switch
            {
                UUAVError.DecodeFailed => ErrorCode.DecodeFailed,
                UUAVError.OpenFailed => ErrorCode.LoadFailed,
                _ => ErrorCode.None,
            };

        // The consumer reads only Count as a "buffered range exists" gate, so
        // the two cached instances cover it without per-poll allocations.
        public TimeRanges GetBufferedTimes() =>
            player.State is UUAVState.Ready or UUAVState.Playing or UUAVState.Paused or UUAVState.Ended
                ? BUFFERED
                : NOT_BUFFERED;

        public double GetDuration() => player.Duration;

        public Texture? GetTexture(int index = 0) => player.CurrentTexture;

        // UUAV's NV12ToRGB shader already flips vertically, so the output
        // RenderTexture is upright.
        public bool RequiresVerticalFlip() => false;
    }
}
