using UnityEngine;

namespace UUAV.Compat
{
    internal sealed class UUAVBackend : IMediaControl, IMediaInfo, ITextureProducer
    {
        private static readonly TimeRanges NotBuffered = new TimeRanges(0);
        private static readonly TimeRanges Buffered = new TimeRanges(1);

        private readonly UUAVPlayer player;
        private bool seekRequested;

        // A seek issued while the media is still opening. The core only
        // accepts seeks once it holds a playback unit, so the target waits
        // here and is issued from Tick as soon as the media settles; a later
        // Seek replaces it, matching how the core coalesces rapid seeks.
        private double? pendingSeek;

        public UUAVBackend(UUAVPlayer player)
        {
            this.player = player;
        }

        public void Tick()
        {
            UUAVState state = player.State;
            bool settled = state == UUAVState.Playing
                || state == UUAVState.Paused
                || state == UUAVState.Ready;

            if (pendingSeek is double target)
            {
                if (settled || state == UUAVState.Ended)
                {
                    pendingSeek = null;
                    player.Seek(target);
                    // seekRequested stays up until a later settled tick
                    return;
                }

                if (state != UUAVState.Opening)
                {
                    // the media went away before it opened
                    pendingSeek = null;
                    seekRequested = false;
                }

                return;
            }

            if (seekRequested && settled)
            {
                seekRequested = false;
            }
        }

        public void Play() => player.Play();

        public void Pause() => player.Pause();

        public void Stop()
        {
            player.Pause();
            Seek(0); // Stop rewinds to the start, keeping the media open
        }

        public void Seek(double time)
        {
            seekRequested = true;

            if (player.State == UUAVState.Opening)
            {
                pendingSeek = time;
                return;
            }

            pendingSeek = null;
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
