// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using UnityEngine;

namespace UnitedAV
{
    public interface IMediaControl
    {
        bool Play();
        bool Pause();
        bool Stop();

        bool Seek(double timeSeconds);

        void SetLooping(bool looping);
        bool IsLooping();

        void SetPlaybackRate(float rate);
        float GetPlaybackRate();

        bool IsPlaying();
        bool IsPaused();
        bool IsFinished();
        bool IsSeeking();
        bool IsBuffering();

        double GetCurrentTime();
        TimeRanges GetBufferedTimes();
        ErrorCode GetLastError();
    }

    public interface IMediaInfo
    {
        // double.PositiveInfinity for live streams or unknown duration.
        double GetDuration();

        int GetVideoWidth();
        int GetVideoHeight();
        float GetVideoFrameRate();

        bool HasVideo();
        bool HasAudio();
    }

    public interface ITextureProducer
    {
        // Null while loading; the reference may change across videos or device loss.
        Texture GetTexture();

        // True when rows are top-down and need a vertical flip in Unity's bottom-up UV space.
        bool RequiresVerticalFlip();
    }

    public struct TimeRange
    {
        public double startTime;
        public double duration;

        public TimeRange(double start, double dur)
        {
            startTime = start;
            duration = dur;
        }

        public double EndTime => startTime + duration;
    }

    public sealed class TimeRanges
    {
        private readonly List<TimeRange> _ranges;

        public TimeRanges()
        {
            _ranges = new List<TimeRange>();
        }

        public TimeRanges(List<TimeRange> ranges)
        {
            _ranges = ranges ?? new List<TimeRange>();
        }

        public int Count => _ranges.Count;

        public TimeRange this[int index] => _ranges[index];

        public IReadOnlyList<TimeRange> Ranges => _ranges;
    }
}
