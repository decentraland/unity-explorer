// SPDX-License-Identifier: Apache-2.0

using UnitedAV.Internal;

namespace UnitedAV
{
    internal sealed class MediaInfoImpl : IMediaInfo
    {
        private readonly MediaPlayer _player;

        public MediaInfoImpl(MediaPlayer player)
        {
            _player = player;
        }

        private System.IntPtr H => _player.NativeHandle;

        private bool TryGetInfo(out UAVMediaInfo info)
        {
            info = default;
            if (H == System.IntPtr.Zero)
                return false;
            return UnitedAVNative.uav_get_info(H, out info) == (int)UAVResult.Ok;
        }

        public double GetDuration()
        {
            if (!TryGetInfo(out var info))
                return double.PositiveInfinity;

            return info.duration > 0.0 ? info.duration : double.PositiveInfinity;
        }

        public int GetVideoWidth()
        {
            return TryGetInfo(out var info) ? info.width : 0;
        }

        public int GetVideoHeight()
        {
            return TryGetInfo(out var info) ? info.height : 0;
        }

        public float GetVideoFrameRate()
        {
            return TryGetInfo(out var info) ? (float)info.frame_rate : 0f;
        }

        public bool HasVideo()
        {
            return TryGetInfo(out var info) && info.has_video != 0;
        }

        public bool HasAudio()
        {
            return TryGetInfo(out var info) && info.has_audio != 0;
        }
    }
}
