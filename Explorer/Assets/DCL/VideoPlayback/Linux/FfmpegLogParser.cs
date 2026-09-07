#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using System.Globalization;
using System.Text.RegularExpressions;

namespace DCL.VideoPlayback
{
    /// <summary>
    /// Line parsers for the decoder's stderr: the input summary printed when a
    /// source opens (duration, streams) and the per-frame <c>showinfo</c> lines
    /// that carry each video frame's timestamp and size.
    /// </summary>
    internal static class FfmpegLogParser
    {
        private static readonly Regex SHOW_INFO = new (
            @"\bn:\s*(?<n>\d+)\s+pts:\s*(?<pts>-?\d+|N/A)\s+pts_time:(?<time>-?\d+(?:\.\d+)?|N/A|nan)\b.*?\bs:(?<w>\d+)x(?<h>\d+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex DURATION = new (
            @"^\s*Duration:\s*(?:(?<na>N/A)|(?<h>\d+):(?<m>\d+):(?<s>\d+(?:\.\d+)?))",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex STREAM = new (
            @"^\s*Stream #0:(?<index>\d+)(?:\[[^\]]*\])?(?:\([^)]*\))?: (?<kind>Video|Audio): (?<details>.*)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex RESOLUTION = new (
            @"(?<![\w.])(?<w>\d{2,5})x(?<h>\d{2,5})(?![\w])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex FPS = new (
            @"(?<fps>\d+(?:\.\d+)?)\s*fps\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public readonly struct ShowInfo
        {
            public readonly int Index;
            public readonly bool HasTime;
            public readonly double Time;
            public readonly int Width;
            public readonly int Height;

            public ShowInfo(int index, bool hasTime, double time, int width, int height)
            {
                Index = index;
                HasTime = hasTime;
                Time = time;
                Width = width;
                Height = height;
            }
        }

        public static bool TryParseShowInfo(string line, out ShowInfo info)
        {
            Match match = SHOW_INFO.Match(line);

            if (!match.Success)
            {
                info = default;
                return false;
            }

            bool hasTime = double.TryParse(match.Groups["time"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double time);

            info = new ShowInfo(
                int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture),
                hasTime,
                hasTime ? time : 0d,
                int.Parse(match.Groups["w"].Value, CultureInfo.InvariantCulture),
                int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture));

            return true;
        }

        /// <summary>Returns true for a Duration line; <paramref name="seconds"/> is zero when the source reports no duration (live).</summary>
        public static bool TryParseDuration(string line, out double seconds)
        {
            seconds = 0d;
            Match match = DURATION.Match(line);
            if (!match.Success) return false;
            if (match.Groups["na"].Success) return true;

            seconds = (int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture) * 3600)
                      + (int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture) * 60)
                      + double.Parse(match.Groups["s"].Value, NumberStyles.Float, CultureInfo.InvariantCulture);

            return true;
        }

        public static bool TryParseStream(string line, out ProbeStream stream)
        {
            Match match = STREAM.Match(line);

            if (!match.Success)
            {
                stream = default;
                return false;
            }

            int index = int.Parse(match.Groups["index"].Value, CultureInfo.InvariantCulture);
            string details = match.Groups["details"].Value;

            if (match.Groups["kind"].Value != "Video")
            {
                stream = ProbeStream.Audio(index);
                return true;
            }

            Match resolution = RESOLUTION.Match(details);
            int width = resolution.Success ? int.Parse(resolution.Groups["w"].Value, CultureInfo.InvariantCulture) : 0;
            int height = resolution.Success ? int.Parse(resolution.Groups["h"].Value, CultureInfo.InvariantCulture) : 0;

            Match fps = FPS.Match(details);
            double frameRate = fps.Success ? double.Parse(fps.Groups["fps"].Value, NumberStyles.Float, CultureInfo.InvariantCulture) : 0d;

            stream = ProbeStream.Video(index, width, height, frameRate);
            return true;
        }
    }
}
#endif
