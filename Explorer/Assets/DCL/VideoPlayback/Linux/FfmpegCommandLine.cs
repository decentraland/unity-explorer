#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace DCL.VideoPlayback
{
    /// <summary>
    /// Locates the ffmpeg binary and builds the argument strings for the probe
    /// and playback processes.
    /// </summary>
    internal static class FfmpegCommandLine
    {
        public const string BINARY_ENV = "DCL_FFMPEG_BIN";
        public const string BINARY_NAME = "ffmpeg";

        // atempo keeps pitch while stretching time but only accepts this span per
        // instance, so slower rates chain two of them.
        public const float MIN_RATE = 0.25f;
        public const float MAX_RATE = 4f;
        private const float ATEMPO_MIN = 0.5f;

        // 15 s of no bytes on a network read is treated as a dead connection, so a
        // stalled CDN surfaces as an error instead of a frozen frame forever.
        private const string NETWORK_INPUT_OPTIONS = "-reconnect 1 -reconnect_streamed 1 -reconnect_on_network_error 1 -reconnect_delay_max 5 -rw_timeout 15000000";

        private static readonly string[] NETWORK_SCHEMES = { "http://", "https://", "rtmp://", "rtmps://", "rtsp://", "srt://" };

        /// <summary>
        /// Resolution order: the DCL_FFMPEG_BIN override, a binary shipped next to
        /// the player (Plugins/x86_64 or beside the executable), then PATH.
        /// Returns null when none of them exists.
        /// </summary>
        public static string? ResolveBinary(string dataPath)
        {
            if (Environment.GetEnvironmentVariable(BINARY_ENV) is { Length: > 0 } overridePath)
                return File.Exists(overridePath) ? overridePath : null;

            string bundled = Path.Combine(dataPath, "Plugins", "x86_64", BINARY_NAME);
            if (File.Exists(bundled)) return bundled;

            string beside = Path.Combine(Path.GetDirectoryName(dataPath) ?? dataPath, BINARY_NAME);
            if (File.Exists(beside)) return beside;

            if (Environment.GetEnvironmentVariable("PATH") is not { Length: > 0 } pathVariable) return null;

            foreach (string dir in pathVariable.Split(':'))
            {
                if (dir.Length == 0) continue;
                string candidate = Path.Combine(dir, BINARY_NAME);
                if (File.Exists(candidate)) return candidate;
            }

            return null;
        }

        public static bool IsNetworkSource(string source)
        {
            foreach (string scheme in NETWORK_SCHEMES)
                if (source.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        public static string ProbeArguments(string source)
        {
            var args = new StringBuilder("-hide_banner -nostdin -nostats -loglevel info ");
            AppendInputOptions(args, source);
            args.Append("-i ").Append(Quote(source));
            return args.ToString();
        }

        /// <summary>
        /// One process decodes both streams so their output stays interleaved in
        /// time: video as a stream of 32-bit BMPs (self-describing size) tagged by
        /// <c>showinfo</c> on stderr, audio as interleaved float PCM at Unity's
        /// mixer rate and speaker layout.
        /// </summary>
        public static string PlaybackArguments(string source, MediaProbe probe, double startTime, float rate,
            int sampleRate, int channels, string? videoFifo, string? audioFifo)
        {
            // -y: the output paths are pre-created FIFOs, which ffmpeg would otherwise refuse as existing files.
            var args = new StringBuilder("-hide_banner -nostdin -nostats -loglevel info -y ");
            AppendInputOptions(args, source);

            if (startTime > 0d)
                args.Append("-ss ").Append(startTime.ToString("0.000", CultureInfo.InvariantCulture)).Append(' ');

            args.Append("-i ").Append(Quote(source));

            if (probe.HasVideo && videoFifo != null)
            {
                args.Append(" -map 0:").Append(probe.VideoStream.ToString(CultureInfo.InvariantCulture));
                args.Append(" -vf ").Append(Quote(VideoFilter()));
                args.Append(" -fps_mode passthrough -f image2pipe -c:v bmp -pix_fmt bgra ");
                args.Append(Quote(videoFifo));
            }

            if (probe.HasAudio && audioFifo != null)
            {
                args.Append(" -map 0:").Append(probe.AudioStream.ToString(CultureInfo.InvariantCulture));

                string tempo = AtempoChain(rate);
                if (tempo.Length > 0)
                    args.Append(" -af ").Append(Quote(tempo));

                args.Append(" -f f32le -ar ").Append(sampleRate.ToString(CultureInfo.InvariantCulture));
                args.Append(" -ac ").Append(channels.ToString(CultureInfo.InvariantCulture)).Append(' ');
                args.Append(Quote(audioFifo));
            }

            return args.ToString();
        }

        public static float ClampRate(float rate) =>
            float.IsNaN(rate) ? 1f : Math.Min(MAX_RATE, Math.Max(MIN_RATE, rate));

        /// <summary>Empty at 1x; otherwise one or two chained atempo filters covering the rate.</summary>
        public static string AtempoChain(float rate)
        {
            rate = ClampRate(rate);
            if (Math.Abs(rate - 1f) < 0.001f) return string.Empty;

            if (rate >= ATEMPO_MIN)
                return "atempo=" + rate.ToString("0.000", CultureInfo.InvariantCulture);

            float remainder = rate / ATEMPO_MIN;
            return "atempo=" + ATEMPO_MIN.ToString("0.000", CultureInfo.InvariantCulture)
                   + ",atempo=" + remainder.ToString("0.000", CultureInfo.InvariantCulture);
        }

        /// <summary>Wraps an argument in double quotes with the escapes the process launcher's shell-style splitter honours.</summary>
        public static string Quote(string arg)
        {
            var quoted = new StringBuilder(arg.Length + 2);
            quoted.Append('"');

            foreach (char c in arg)
            {
                if (c == '"' || c == '\\')
                    quoted.Append('\\');

                quoted.Append(c);
            }

            quoted.Append('"');
            return quoted.ToString();
        }

        private static string VideoFilter() =>
            "scale=w='min(iw," + MediaProbe.MAX_WIDTH.ToString(CultureInfo.InvariantCulture)
            + ")':h='min(ih," + MediaProbe.MAX_HEIGHT.ToString(CultureInfo.InvariantCulture)
            + ")':force_original_aspect_ratio=decrease:force_divisible_by=2,showinfo";

        private static void AppendInputOptions(StringBuilder args, string source)
        {
            if (IsNetworkSource(source))
                args.Append(NETWORK_INPUT_OPTIONS).Append(' ');
        }
    }
}
#endif
