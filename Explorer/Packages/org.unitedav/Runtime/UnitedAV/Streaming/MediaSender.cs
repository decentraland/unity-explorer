// SPDX-License-Identifier: Apache-2.0
// Managed binding for the upstream sender. cdecl, library "UnitedAV". A sender is
// not internally synchronized: serialize all calls for a given handle on one thread.

using System;
using System.Runtime.InteropServices;

namespace UnitedAV.Streaming
{
    /// <summary>Video encoder selection.</summary>
    public enum UAVVideoCodec
    {
        None = 0,
        VP9  = 1,
        VP8  = 2,
        AV1  = 3,
    }

    /// <summary>Audio encoder selection.</summary>
    public enum UAVAudioCodec
    {
        None = 0,
        Opus = 1,
    }

    /// <summary>Sender result / error codes. 0 == OK.</summary>
    public enum UAVSendResult
    {
        Ok            = 0,
        ErrInvalid    = -1,
        ErrOpenFailed = -2,
        ErrNoStream   = -3,
        ErrEncode     = -4,
        ErrUnsupported = -5,
        ErrNoMem      = -6,
    }

    /// <summary>Sender configuration; zero-init then fill the fields you need (codec None disables that media).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct UAVSendConfig
    {
        public int video_codec;
        public int width;
        public int height;
        public int fps;
        public int video_bitrate;

        public int audio_codec;
        public int sample_rate;
        public int channels;
        public int audio_bitrate;

        /// <summary>VP9 video + Opus audio, geometry/fps from the first pushed frame.</summary>
        public static UAVSendConfig Default()
        {
            return new UAVSendConfig
            {
                video_codec = (int)UAVVideoCodec.VP9,
                width = 0,
                height = 0,
                fps = 30,
                video_bitrate = 0,
                audio_codec = (int)UAVAudioCodec.Opus,
                sample_rate = 48000,
                channels = 2,
                audio_bitrate = 0,
            };
        }
    }

    internal static class UnitedAVSendNative
    {
        public const string Lib = "UnitedAV";

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint uav_send_abi_version();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr uav_send_create();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void uav_send_destroy(IntPtr s);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi,
            BestFitMapping = false, ThrowOnUnmappableChar = false)]
        public static extern int uav_send_open(IntPtr s,
            [MarshalAs(UnmanagedType.LPStr)] string url, ref UAVSendConfig cfg);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uav_send_push_video(IntPtr s, IntPtr rgba,
            int w, int h, int stride, double pts_seconds);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uav_send_push_audio(IntPtr s, IntPtr interleaved,
            int frames, int channels, int sample_rate, double pts_seconds);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uav_send_close(IntPtr s);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uav_send_last_error(IntPtr s);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int uav_send_get_sdp(IntPtr s, byte[] buf, int buflen);
    }

    /// <summary>Managed wrapper over the upstream sender; one instance owns one native handle, serialize pushes on a single thread.</summary>
    public sealed class MediaSender : IDisposable
    {
        private IntPtr _native = IntPtr.Zero;
        private bool _open;

        public static uint AbiVersion => UnitedAVSendNative.uav_send_abi_version();

        public bool IsOpen => _open;

        public MediaSender()
        {
            _native = UnitedAVSendNative.uav_send_create();
            if (_native == IntPtr.Zero)
                throw new InvalidOperationException(
                    "[UnitedAV] uav_send_create() returned null; native plugin missing or out of memory.");
        }

        /// <summary>Open the output URL with the given config; call once before any push.</summary>
        public UAVSendResult Open(string url, UAVSendConfig config)
        {
            if (_native == IntPtr.Zero)
                return UAVSendResult.ErrInvalid;
            if (string.IsNullOrEmpty(url))
                return UAVSendResult.ErrInvalid;

            int rc = UnitedAVSendNative.uav_send_open(_native, url, ref config);
            _open = rc == (int)UAVSendResult.Ok;
            return (UAVSendResult)rc;
        }

        /// <summary>Push one RGBA8888 frame from a managed array (stride bytes/row, monotonic ptsSeconds).</summary>
        public UAVSendResult PushVideo(byte[] rgba, int w, int h, int stride, double ptsSeconds)
        {
            if (rgba == null)
                return UAVSendResult.ErrInvalid;
            int needed = (stride > 0 ? stride : w * 4) * h;
            if (rgba.Length < needed)
                return UAVSendResult.ErrInvalid;

            GCHandle handle = GCHandle.Alloc(rgba, GCHandleType.Pinned);
            try
            {
                return PushVideo(handle.AddrOfPinnedObject(), w, h, stride, ptsSeconds);
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>Push one RGBA8888 frame from a native pointer (no managed copy).</summary>
        public UAVSendResult PushVideo(IntPtr rgba, int w, int h, int stride, double ptsSeconds)
        {
            if (_native == IntPtr.Zero || !_open || rgba == IntPtr.Zero)
                return UAVSendResult.ErrInvalid;

            int rc = UnitedAVSendNative.uav_send_push_video(_native, rgba, w, h, stride, ptsSeconds);
            return (UAVSendResult)rc;
        }

        /// <summary>Push interleaved float audio (~[-1,1]) from a managed array; frames is samples-per-channel.</summary>
        public UAVSendResult PushAudio(float[] interleaved, int frames, int channels, int sampleRate, double ptsSeconds)
        {
            if (interleaved == null || frames <= 0 || channels <= 0)
                return UAVSendResult.ErrInvalid;
            if (interleaved.Length < frames * channels)
                return UAVSendResult.ErrInvalid;

            GCHandle handle = GCHandle.Alloc(interleaved, GCHandleType.Pinned);
            try
            {
                return PushAudio(handle.AddrOfPinnedObject(), frames, channels, sampleRate, ptsSeconds);
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>Push interleaved float audio from a native pointer.</summary>
        public UAVSendResult PushAudio(IntPtr interleaved, int frames, int channels, int sampleRate, double ptsSeconds)
        {
            if (_native == IntPtr.Zero || !_open || interleaved == IntPtr.Zero)
                return UAVSendResult.ErrInvalid;

            int rc = UnitedAVSendNative.uav_send_push_audio(
                _native, interleaved, frames, channels, sampleRate, ptsSeconds);
            return (UAVSendResult)rc;
        }

        /// <summary>Flush encoders, write the trailer, close the output; the handle can be reopened afterward.</summary>
        public UAVSendResult Close()
        {
            if (_native == IntPtr.Zero)
                return UAVSendResult.ErrInvalid;
            int rc = UnitedAVSendNative.uav_send_close(_native);
            _open = false;
            return (UAVSendResult)rc;
        }

        /// <summary>Last error code recorded by an open/push/close on this handle.</summary>
        public UAVSendResult LastError()
        {
            if (_native == IntPtr.Zero)
                return UAVSendResult.ErrInvalid;
            return (UAVSendResult)UnitedAVSendNative.uav_send_last_error(_native);
        }

        /// <summary>SDP for the current RTP session (only meaningful for rtp:// URLs), or null if unavailable.</summary>
        public string GetSdp()
        {
            if (_native == IntPtr.Zero)
                return null;

            int needed = UnitedAVSendNative.uav_send_get_sdp(_native, null, 0);
            if (needed <= 0)
                return null;

            byte[] buf = new byte[needed + 1];
            int written = UnitedAVSendNative.uav_send_get_sdp(_native, buf, buf.Length);
            if (written <= 0)
                return null;

            int len = Math.Min(written, buf.Length);
            if (len > 0 && buf[len - 1] == 0)
                len--;
            return System.Text.Encoding.ASCII.GetString(buf, 0, len);
        }

        /// <summary>Close (if open) and destroy the native handle; safe to call twice.</summary>
        public void Dispose()
        {
            if (_native != IntPtr.Zero)
            {
                if (_open)
                    UnitedAVSendNative.uav_send_close(_native);
                _open = false;
                UnitedAVSendNative.uav_send_destroy(_native);
                _native = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }

        ~MediaSender()
        {
            if (_native != IntPtr.Zero)
            {
                UnitedAVSendNative.uav_send_destroy(_native);
                _native = IntPtr.Zero;
            }
        }
    }
}
