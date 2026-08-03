using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace UUAV
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ErrorCallback(IntPtr message);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void LogCallback(IntPtr message);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void FetchProvider(IntPtr exchange);

    public static class FetchOp
    {
        public const uint Open = 1;
        public const uint Read = 2;
        public const uint Close = 3;
    }

    public static class FetchStatus
    {
        public const uint Ok = 0;
        public const uint Eof = 1;
        public const uint Err = 2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FetchExchange
    {
        public uint Op;
        public uint Handle;
        public ulong Offset;
        public uint Len;
        public uint Flags;
        public IntPtr Url;
        public uint UrlLen;
        public IntPtr Buf;
        public uint BufCap;
        public uint Status;
        public uint N;
        public long Size;
        public uint OutHandle;
    }

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

    public enum UUAVError
    {
        None = 0,
        OpenFailed = 1,
        DecodeFailed = 4,
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
    public unsafe struct FrameInfo
    {
        private fixed float yuvToRgb[12];
        private fixed float uvTransform[6];

        public uint VisibleWidth;
        public uint VisibleHeight;
        private fixed uint planeWidth[2];
        private fixed uint planeHeight[2];

        public int Colorspace;
        public int ColorRange;
        public int ColorPrimaries;

        public int Rotation;

        public uint BitDepth;

        public ulong FrameIndex;

        public ulong SurfaceGeneration;

        private fixed long planes[2];

        public Vector4 YuvRow(int row)
        {
            fixed (float* m = yuvToRgb)
            {
                return new Vector4(m[row * 4], m[row * 4 + 1], m[row * 4 + 2], m[row * 4 + 3]);
            }
        }

        public Vector4 UvRow(int row)
        {
            fixed (float* t = uvTransform)
            {
                return new Vector4(t[row * 3], t[row * 3 + 1], t[row * 3 + 2], 0f);
            }
        }

        public IntPtr Plane(int plane)
        {
            fixed (long* p = planes)
            {
                return new IntPtr(p[plane]);
            }
        }

        public Vector2Int PlaneSize(int plane)
        {
            fixed (uint* w = planeWidth)
            fixed (uint* h = planeHeight)
            {
                return new Vector2Int((int)w[plane], (int)h[plane]);
            }
        }

        public bool IsRotatedQuarterTurn => Rotation == 90 || Rotation == 270;
    }

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
    public struct AudioSync
    {
        public double MediaTime;
        public double BasePts;
        public double Rate;
        public ulong Generation;
        public ulong FramesConsumed;
        public ulong BaseFrames;
        public ulong SilenceCalls;
        public uint SampleRate;
        public uint Priming;
        public uint HasBasis;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Status
    {
        public ulong PlayersCount;
        private readonly byte initialized;
        public AudioOptions AudioOptions;

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

        public IntPtr ErrorMessage;

        public bool IsOk => ErrorMessage == IntPtr.Zero;

        public string? ConsumeError()
        {
            return Utf8.ConsumeCString(ref ErrorMessage);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ResultFFI
    {
        public IntPtr ErrorMessage;

        public bool IsOk => ErrorMessage == IntPtr.Zero;

        public string? ConsumeError()
        {
            return Utf8.ConsumeCString(ref ErrorMessage);
        }
    }

    internal static class NativeMethods
    {
        private const string Lib = "uuav";

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
        public static extern void uuav_set_fetch_provider(FetchProvider? provider);

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


        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_play(ulong playerId);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_pause(ulong playerId);

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

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_close_media(ulong playerId);


        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern UUAVState uuav_player_state(ulong playerId);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong uuav_player_get_last_error(ulong playerId);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_duration(ulong playerId, out double duration);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_current_time(ulong playerId, out double time);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern byte uuav_player_audio_sync(ulong playerId, out AudioSync sync);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern byte uuav_player_set_presentation_clock(
            ulong playerId,
            double mediaTime
        );

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_assign_master_clock(
            ulong playerId,
            double currentTime
        );

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_get_media_info(
            ulong playerId,
            out MediaInfo info
        );

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_get_frame_info(
            ulong playerId,
            out FrameInfo info
        );

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_current_controls_state(
            ulong playerId,
            out ControlsState state
        );


        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_seek_async(ulong playerId, double time);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_set_looping(
            ulong playerId,
            [MarshalAs(UnmanagedType.U1)] bool looping
        );

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool uuav_player_get_looping(ulong playerId);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ResultFFI uuav_player_set_rate(ulong playerId, double rate);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern double uuav_player_get_rate(ulong playerId);


        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr uuav_get_render_callback();


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
