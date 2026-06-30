// SPDX-License-Identifier: Apache-2.0
// P/Invoke layer over the native C ABI. cdecl, library "UnitedAV".

using System;
using System.Runtime.InteropServices;

namespace UnitedAV.Internal
{
    internal enum UAVState
    {
        Idle      = 0,
        Opening   = 1,
        Ready     = 2,
        Playing   = 3,
        Paused    = 4,
        Buffering = 5,
        Finished  = 6,
        Error     = 7,
    }

    internal enum UAVResult
    {
        Ok            = 0,
        ErrInvalid    = -1,
        ErrOpenFailed = -2,
        ErrNoStream   = -3,
        ErrDecode     = -4,
        ErrUnsupported = -5,
        ErrNoMem      = -6,
    }

    internal enum UAVPixelFormat
    {
        Rgba32 = 0,
        Nv12   = 1,
    }

    // data is owned by the native player and valid only until the matching uav_release_frame.
    [StructLayout(LayoutKind.Sequential)]
    internal struct UAVVideoFrame
    {
        public IntPtr data;
        public int    width;
        public int    height;
        public int    stride;
        public int    format;
        public long   frame_id;
        public double pts;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct UAVMediaInfo
    {
        public int    has_video;
        public int    has_audio;
        public int    width;
        public int    height;
        public double frame_rate;
        public double duration;
        public int    audio_channels;
        public int    audio_sample_rate;
    }

    internal static class UnitedAVNative
    {
        public const string Lib = "UnitedAV";

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint uav_abi_version();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr uav_create();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void uav_destroy(IntPtr p);

        // BestFitMapping/ThrowOnUnmappableChar disabled so URL bytes pass through unmodified.
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi,
            BestFitMapping = false, ThrowOnUnmappableChar = false)]
        public static extern int uav_open(IntPtr p, [MarshalAs(UnmanagedType.LPStr)] string url);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uav_close(IntPtr p);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uav_play(IntPtr p);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uav_pause(IntPtr p);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uav_stop(IntPtr p);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uav_seek(IntPtr p, double seconds);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uav_set_looping(IntPtr p, int loop);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uav_set_rate(IntPtr p, float rate);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uav_set_volume(IntPtr p, float volume);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uav_set_muted(IntPtr p, int muted);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uav_get_state(IntPtr p);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern double uav_get_position(IntPtr p);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uav_get_info(IntPtr p, out UAVMediaInfo outInfo);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uav_last_error(IntPtr p);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uav_acquire_frame(IntPtr p, long last_frame_id, out UAVVideoFrame outFrame);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void uav_release_frame(IntPtr p);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uav_read_audio(IntPtr p, IntPtr dst, int frames, int channels, int sample_rate);
    }
}
