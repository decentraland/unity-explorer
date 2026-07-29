using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace UUAV
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ErrorCallback(IntPtr message);

    // FFmpeg log sinks: one already-formatted, NUL-terminated line per call.
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void LogCallback(IntPtr message);

    // Mirrors FFmpeg's lavu_log_constants; cast to int at the FFI boundary.
    public enum UUAVLogLevel
    {
        Quiet = -8,
        Panic = 0,
        Fatal = 8,
        Error = 16,
        Warning = 24,
        Info = 32,
        Verbose = 40,
        Debug = 48,
        Trace = 56,
    }

    public enum UUAVState
    {
        Closed,
        Opening,
        Ready,
        Playing,
        Paused,
        Ended,
        Error,
        Unknown,
    }

    public static class UUAVStateExtensions
    {
        public static string ToStringNoAlloc(this UUAVState state)
        {
            return state switch
            {
                UUAVState.Closed => "Closed",
                UUAVState.Opening => "Opening",
                UUAVState.Ready => "Ready",
                UUAVState.Playing => "Playing",
                UUAVState.Paused => "Paused",
                UUAVState.Ended => "Ended",
                UUAVState.Error => "Error",
                UUAVState.Unknown => "Unknown",
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AudioOptions
    {
        public int SampleRate;
        public int Channels;

        public static AudioOptions FromConfig(AudioConfiguration config)
        {
            return new AudioOptions
            {
                SampleRate = config.sampleRate,
                Channels = ChannelCount(config.speakerMode),
            };
        }

        private static int ChannelCount(AudioSpeakerMode mode)
        {
            return mode switch
            {
                AudioSpeakerMode.Mono => 1,
                AudioSpeakerMode.Stereo => 2,
                AudioSpeakerMode.Quad => 4,
                AudioSpeakerMode.Surround => 5,
                AudioSpeakerMode.Mode5point1 => 6,
                AudioSpeakerMode.Mode7point1 => 8,
                _ => 2
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
    public struct VideoSize
    {
        public uint Width;
        public uint Height;
    }

    // Snapshot of the open media's source stream parameters, captured at
    // open time. Name fields are NUL-terminated UTF-8; unknown values are
    // 0 / empty, Duration is -1 for realtime streams. Video fields are
    // zero when HasVideo is false, audio fields when HasAudio is false.
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct MediaInfo
    {
        private const int NameLen = 32;

        public double Duration;
        public double Framerate;
        public long VideoBitrate;
        public long AudioBitrate;
        public uint Width;
        public uint Height;
        public int SampleRate;
        public int Channels;
        private fixed byte videoCodec[NameLen];
        private fixed byte pixelFormat[NameLen];
        private fixed byte audioCodec[NameLen];
        private fixed byte sampleFormat[NameLen];
        private readonly byte hasVideo;
        private readonly byte hasAudio;

        public bool HasVideo => hasVideo != 0;

        public bool HasAudio => hasAudio != 0;

        // e.g. "h264"
        public string VideoCodec
        {
            get
            {
                fixed (byte* p = videoCodec)
                {
                    return Utf8.FixedToString(p, NameLen);
                }
            }
        }

        // e.g. "yuv420p"
        public string PixelFormat
        {
            get
            {
                fixed (byte* p = pixelFormat)
                {
                    return Utf8.FixedToString(p, NameLen);
                }
            }
        }

        // e.g. "aac"
        public string AudioCodec
        {
            get
            {
                fixed (byte* p = audioCodec)
                {
                    return Utf8.FixedToString(p, NameLen);
                }
            }
        }

        // e.g. "fltp"
        public string SampleFormat
        {
            get
            {
                fixed (byte* p = sampleFormat)
                {
                    return Utf8.FixedToString(p, NameLen);
                }
            }
        }
    }

    // Snapshot of the user-facing control values: the latest pushed
    // intents, which the playback thread applies asynchronously. A
    // *Pending flag means that control's latest push has not been
    // consumed by the playback thread yet.
    [StructLayout(LayoutKind.Sequential)]
    public struct ControlsState
    {
        public double Rate;
        private readonly byte play;
        private readonly byte playPending;
        private readonly byte looping;
        private readonly byte loopingPending;
        private readonly byte ratePending;

        public bool Play => play != 0;
        public bool PlayPending => playPending != 0;
        public bool Looping => looping != 0;
        public bool LoopingPending => loopingPending != 0;
        public bool RatePending => ratePending != 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Status
    {
        public ulong PlayersCount;
        private readonly byte initialized;
        public AudioOptions AudioOptions;

        // allocated by the native side; release via ConsumeError
        public IntPtr DeviceRemoveReason;

        public bool Initialized => initialized != 0;

        public string? ConsumeDeviceRemoveReason()
        {
            return Utf8.ConsumeCString(ref DeviceRemoveReason);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NewPlayerResult
    {
        public ulong PlayerId;

        // allocated by the native side; release via ConsumeError
        public IntPtr ErrorMessage;

        public bool IsOk => ErrorMessage == IntPtr.Zero;

        // reads the message and frees it on the native side; call at most once
        public string? ConsumeError()
        {
            return Utf8.ConsumeCString(ref ErrorMessage);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ResultFFI
    {
        // allocated by the native side; release via ConsumeError
        public IntPtr ErrorMessage;

        public bool IsOk => ErrorMessage == IntPtr.Zero;

        // reads the message and frees it on the native side; call at most once
        public string? ConsumeError()
        {
            return Utf8.ConsumeCString(ref ErrorMessage);
        }
    }

    internal static class NativeMethods
    {
        private const string Lib = "uuav";

        // must be called exactly once per non-null message
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void uuav_string_free(IntPtr str);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr uuav_abi_version();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_init(
            IntPtr probe_texture,
            AudioOptions audioOptions,
            ErrorCallback? errorCallback,
            LogCallback? warningCallback,
            LogCallback? logCallback,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string protocolWhitelist,
            int logLevel
        );

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void uuav_set_log_level(int level);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void uuav_deinit();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_update_audio_out(AudioOptions options);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern Status uuav_status();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern NewPlayerResult uuav_player_new();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void uuav_player_free(ulong playerId);

        // ---- lifecycle -------------------------------------------------

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_play(ulong playerId);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_pause(ulong playerId);

        // async! returns immediately
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern ResultFFI uuav_player_open_media_async(ulong playerId, IntPtr url);

        public static ResultFFI uuav_player_open_media_async(ulong playerId, string url)
        {
            var urlPtr = Utf8.AllocCString(url);
            try
            {
                return uuav_player_open_media_async(playerId, urlPtr);
            }
            finally
            {
                Marshal.FreeHGlobal(urlPtr);
            }
        }

        // back to CLOSED, player reusable
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_close_media(ulong playerId);

        // ---- state -----------------------------------------------------

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern UUAVState uuav_player_state(ulong playerId);

        // valid from READY; may be unavailable for realtime streams
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_duration(ulong playerId, out double duration);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_current_time(ulong playerId, out double time);

        // the player slaves its playback to the externally provided master clock
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_assign_master_clock(
            ulong playerId,
            double currentTime
        );

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_get_video_size(
            ulong playerId,
            out VideoSize size
        );

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_get_media_info(
            ulong playerId,
            out MediaInfo info
        );

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_current_controls_state(
            ulong playerId,
            out ControlsState state
        );

        // ---- transport (commands: set flags, decoder thread obeys) ------

        // async; coalesces repeated calls
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_seek_async(ulong playerId, double time);

        // persists across url switches
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_set_looping(
            ulong playerId,
            [MarshalAs(UnmanagedType.U1)] bool looping
        );

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool uuav_player_get_looping(ulong playerId);

        // Not applicable for realtime streams
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_set_rate(ulong playerId, double rate);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern double uuav_player_get_rate(ulong playerId);

        // ---- video -------------------------------------------------------

        // pass to GL.IssuePluginEvent / CommandBuffer.IssuePluginEvent
        // with the player id as the event id
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr uuav_get_render_callback();

        // SRV: plane 0 = Y, 1 = UV; valid from READY; recreated on resolution change
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_get_video_texture(
            ulong playerId,
            int plane,
            out IntPtr texture
        );

        // ---- audio -------------------------------------------------------

        // [audio] fills interleaved FLT; pads silence on underrun,
        // never blocks; returns frames actually copied
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uuav_player_read_audio(
            ulong playerId,
            [Out] float[] dst,
            int nbFrames
        );
    }

    internal static class Utf8
    {
        public static unsafe string? PtrToString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
            {
                return null;
            }

            var bytes = (byte*)ptr;
            var length = 0;
            while (bytes[length] != 0)
            {
                length++;
            }

            return Encoding.UTF8.GetString(bytes, length);
        }

        // reads a NUL-terminated UTF-8 string from a fixed-size buffer
        public static unsafe string FixedToString(byte* buffer, int capacity)
        {
            var length = 0;
            while (length < capacity && buffer[length] != 0)
            {
                length++;
            }

            return Encoding.UTF8.GetString(buffer, length);
        }

        public static IntPtr AllocCString(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var ptr = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            Marshal.WriteByte(ptr, bytes.Length, 0);
            return ptr;
        }

        public static string? ConsumeCString(ref IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
            {
                return null;
            }
            
            var message = Utf8.PtrToString(ptr);
            NativeMethods.uuav_string_free(ptr);
            ptr = IntPtr.Zero;
            return message;
        }
    }
}
