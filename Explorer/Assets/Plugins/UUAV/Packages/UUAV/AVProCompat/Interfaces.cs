using UnityEngine;

namespace UUAV.Compat
{
    // The subset of AVPro's IMediaControl the consumer polls. Members AVPro
    // exposes but the consumer never calls (SeekFast, volume/mute, audio-360,
    // external playback, ...) are intentionally omitted.
    public interface IMediaControl
    {
        void Play();
        void Pause();
        void Stop();
        void Seek(double time);

        bool IsPlaying();
        bool IsPaused();
        bool IsSeeking();
        bool IsBuffering();
        bool IsFinished();

        bool IsLooping();
        void SetLooping(bool looping);

        double GetCurrentTime();

        float GetPlaybackRate();
        void SetPlaybackRate(float rate);

        ErrorCode GetLastError();

        TimeRanges GetBufferedTimes();
    }

    // The consumer reads only the duration off IMediaInfo (it takes width/height
    // from the produced texture instead).
    public interface IMediaInfo
    {
        double GetDuration();
    }

    // The consumer uses only GetTexture + RequiresVerticalFlip.
    public interface ITextureProducer
    {
        Texture? GetTexture(int index = 0);
        bool RequiresVerticalFlip();
    }

    // AVPro's TimeRanges exposes an indexer and time bounds; the consumer only
    // reads Count, using "buffered range exists" as its readiness gate before
    // applying playback properties.
    public sealed class TimeRanges
    {
        public int Count { get; }

        public TimeRanges(int count)
        {
            Count = count;
        }
    }
}
