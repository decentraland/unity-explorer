using System;
using System.Globalization;
using UUAV;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     Builds the audio-health row strings for the Media Player debug tab.
    ///     Pure formatting over already-fetched stats, so tests can exercise it
    ///     without the native library.
    /// </summary>
    internal static class MediaPlayerAudioDebugFormatter
    {
        /// <summary>
        ///     Above the helper's max audio catch-up per pull (100 ms) a serve-loop
        ///     iteration loses wall-clock coverage for feedback-less consumers.
        /// </summary>
        private const ulong SERVE_ITER_RED_US = 100_000;

        /// <summary>Client jitter ring: fill level, gaps, watermark drops.</summary>
        public static string JitterRow(in AudioStats stats, bool isPlaying, bool underrunsGrew, bool watermarkGrew)
        {
            string underruns = Paint(Inv($"underruns {stats.JitterUnderruns}"), underrunsGrew);
            string wmDrop = Paint(Inv($"wm-drop {stats.JitterWatermarkDroppedMs:F0}ms"), watermarkGrew);

            // unprimed while playing = an audible gap right now
            string primed = stats.JitterPrimed ? "primed:y" : Paint("primed:n", isPlaying);

            return Inv($"fill {stats.JitterFillMs:F0}ms | {underruns} | {wmDrop} | {primed}");
        }

        /// <summary>
        ///     Core (helper-side) pipeline. `stalls` grows in normal steady state -
        ///     the decoded ring runs full by design; the starvation signal is a LOW
        ///     ring fill while playing.
        /// </summary>
        public static string CoreRow(in AudioStats stats, bool driftGrew)
        {
            string drift = Paint(Inv($"drift-drop {stats.CoreDriftDroppedMs:F0}ms"), driftGrew);
            return Inv($"ring {stats.CoreRingFillMs:F0}ms | {drift} | silence {stats.CoreSilencePulls} | stalls {stats.CoreRingStalls}");
        }

        /// <summary>Unity DSP callback counters; ch 0 = permanently silent player.</summary>
        public static string DspRow(in UUAVDebug.PlayerInfo player)
        {
            string channels = player.NativeChannels == 0
                ? Paint("ch 0 (no negotiated format - mute)", true)
                : Inv($"ch {player.NativeChannels}");

            return Inv($"{channels} | req {Compact(player.DspFramesRequested)} ret {Compact(player.DspFramesReturned)} | mute {player.DspSilencedCallbacks}");
        }

        /// <summary>Helper serve-loop health (worst iteration, clamped pulls).</summary>
        public static string EngineRow(in EngineAudioStats stats, bool available, bool clampsGrew)
        {
            if (!available)
                return "n/a (stale native binary?)";

            double maxMs = stats.ServeMaxIterUs / 1000.0;
            string serve = Paint(Inv($"serve max {maxMs:F1}ms"), stats.ServeMaxIterUs > SERVE_ITER_RED_US);
            string clamps = Paint(Inv($"pull clamps {stats.AudioPullClamps}"), clampsGrew);
            return $"{serve} | {clamps}";
        }

        private static string Paint(string text, bool red) =>
            red ? $"<color=red>{text}</color>" : text;

        private static string Compact(long value)
        {
            if (value >= 1_000_000)
                return Inv($"{value / 1_000_000.0:F2}M");

            if (value >= 10_000)
                return Inv($"{value / 1_000.0:F1}k");

            return value.ToString(CultureInfo.InvariantCulture);
        }

        // stable output regardless of the user's system locale
        private static string Inv(FormattableString text) =>
            text.ToString(CultureInfo.InvariantCulture);
    }
}
