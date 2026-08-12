using UnityEngine;

namespace UUAV.Compat
{
    internal sealed class UUAVBackend : IMediaControl, IMediaInfo, ITextureProducer
    {
        private static readonly TimeRanges NotBuffered = new TimeRanges(0);
        private static readonly TimeRanges Buffered = new TimeRanges(1);

        private readonly UUAVPlayer player;

        private bool seekRequested;

        public UUAVBackend(UUAVPlayer player)
        {
            this.player = player;
        }

        public void Tick()
        {
            UUAVState state = player.State;

            if (seekRequested
                && (state == UUAVState.Playing
                    || state == UUAVState.Paused
                    || state == UUAVState.Ready))
            {
                seekRequested = false;
            }
        }

        public void Play() => player.Play();

        public void Pause() => player.Pause();

        public void Stop() 
        {
            player.Pause();
            player.Seek(0); // To match AVPro semantics
        }

        public void Seek(double time)
        {
            seekRequested = true;
            player.Seek(time);
        }

        public bool IsPlaying() => player.State == UUAVState.Playing;

        public bool IsPaused() => player.State == UUAVState.Paused;

        // Best-effort: UUAV has no distinct seeking state, so this reflects a
        // pending Seek until the player settles (see Tick).
        public bool IsSeeking() => seekRequested;

        public bool IsBuffering() => player.State == UUAVState.Opening;

        public bool IsFinished() => player.State == UUAVState.Ended;

        public bool IsLooping() => player.Looping;

        public void SetLooping(bool value) => player.Looping = value;

        public double GetCurrentTime() => player.CurrentTime;

        public float GetPlaybackRate() => (float)player.PlaybackRate;

        public void SetPlaybackRate(float rate) => player.PlaybackRate = rate;

        public ErrorCode GetLastError() =>
            player.State == UUAVState.Error ? ErrorCode.LoadFailed : ErrorCode.None;

        // The consumer reads only Count, waiting for a buffered range to exist
        // before applying loop/rate/volume. Report one range once media is ready.
        public TimeRanges GetBufferedTimes()
        {
            switch (player.State)
            {
                case UUAVState.Ready:
                case UUAVState.Playing:
                case UUAVState.Paused:
                case UUAVState.Ended:
                    return Buffered;
                default:
                    return NotBuffered;
            }
        }

        public double GetDuration() => player.Duration;

        public Texture? GetTexture(int index = 0) 
        {
            return player.CurrentTexture;
        }

        // UUAV's NV12ToRGB shader already flips vertically, so the output
        // RenderTexture is upright and no consumer-side flip is needed.
        // Comment is valid as long as behaviour of the UUAVPlayer and shader remain unchanged
        public bool RequiresVerticalFlip() => false;
    }
}
