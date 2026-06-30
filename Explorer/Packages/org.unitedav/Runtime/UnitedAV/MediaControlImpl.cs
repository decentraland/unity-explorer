// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using UnitedAV.Internal;

namespace UnitedAV
{
    internal sealed class MediaControlImpl : IMediaControl
    {
        private readonly MediaPlayer _player;

        public MediaControlImpl(MediaPlayer player)
        {
            _player = player;
        }

        private System.IntPtr H => _player.NativeHandle;
        private bool Valid => H != System.IntPtr.Zero;

        public bool Play()
        {
            if (!Valid) return false;
            return UnitedAVNative.uav_play(H) == (int)UAVResult.Ok;
        }

        public bool Pause()
        {
            if (!Valid) return false;
            return UnitedAVNative.uav_pause(H) == (int)UAVResult.Ok;
        }

        public bool Stop()
        {
            if (!Valid) return false;
            return UnitedAVNative.uav_stop(H) == (int)UAVResult.Ok;
        }

        public bool Seek(double timeSeconds)
        {
            if (!Valid) return false;
            _player.NotifySeekRequested();
            return UnitedAVNative.uav_seek(H, timeSeconds) == (int)UAVResult.Ok;
        }

        public void SetLooping(bool looping)
        {
            _player.LoopingFlag = looping;
            if (Valid)
                UnitedAVNative.uav_set_looping(H, looping ? 1 : 0);
        }

        public bool IsLooping()
        {
            return _player.LoopingFlag;
        }

        public void SetPlaybackRate(float rate)
        {
            if (Valid)
            {
                UnitedAVNative.uav_set_rate(H, rate);
                _player.PlaybackRateValue = rate;
            }
        }

        public float GetPlaybackRate()
        {
            return _player.PlaybackRateValue;
        }

        public bool IsPlaying()
        {
            return Valid && (UAVState)UnitedAVNative.uav_get_state(H) == UAVState.Playing;
        }

        public bool IsPaused()
        {
            return Valid && (UAVState)UnitedAVNative.uav_get_state(H) == UAVState.Paused;
        }

        public bool IsFinished()
        {
            return Valid && (UAVState)UnitedAVNative.uav_get_state(H) == UAVState.Finished;
        }

        public bool IsSeeking()
        {
            if (!Valid) return false;
            return _player.IsSeekingInternal;
        }

        public bool IsBuffering()
        {
            return Valid && (UAVState)UnitedAVNative.uav_get_state(H) == UAVState.Buffering;
        }

        public double GetCurrentTime()
        {
            return Valid ? UnitedAVNative.uav_get_position(H) : 0.0;
        }

        public TimeRanges GetBufferedTimes()
        {
            var ranges = new List<TimeRange>();
            if (Valid)
            {
                double pos = UnitedAVNative.uav_get_position(H);
                if (pos > 0.0)
                    ranges.Add(new TimeRange(0.0, pos));
            }
            return new TimeRanges(ranges);
        }

        public ErrorCode GetLastError()
        {
            if (!Valid)
                return ErrorCode.None;
            return MediaPlayer.MapError(UnitedAVNative.uav_last_error(H));
        }
    }
}
