using NUnit.Framework;
using UUAV;

namespace DCL.SDKComponents.MediaStream.Tests
{
    /// <summary>
    ///     Covers the audio-health row strings of the "Media Player" debug tab.
    ///     Pure formatting - no native library involved.
    /// </summary>
    public class MediaPlayerAudioDebugFormatterShould
    {
        private static AudioStats StatsAt48KStereo()
        {
            return new AudioStats
            {
                JitterSamplesPerSecond = 96_000, // 48 kHz x 2 channels
            };
        }

        private static UUAVDebug.PlayerInfo Player(int nativeChannels, long requested = 0, long returned = 0, long silenced = 0)
        {
            return new UUAVDebug.PlayerInfo(
                playerId: 1,
                UUAVState.Playing,
                url: "https://example.test/stream",
                nativeChannels,
                requested,
                returned,
                silenced,
                hasAudioStats: false,
                audio: default
            );
        }

        [Test]
        public void ConvertJitterSamplesToMilliseconds()
        {
            AudioStats stats = StatsAt48KStereo();
            stats.JitterFillSamples = 9_600; // 100 ms at 96k samples/s

            string row = MediaPlayerAudioDebugFormatter.JitterRow(stats, isPlaying: false, underrunsGrew: false, watermarkGrew: false);

            StringAssert.Contains("fill 100ms", row);
        }

        [Test]
        public void SurviveZeroSampleRateWithoutDividing()
        {
            var stats = new AudioStats
            {
                JitterFillSamples = 12_345,
                JitterSamplesPerSecond = 0,
            };

            string row = MediaPlayerAudioDebugFormatter.JitterRow(stats, isPlaying: true, underrunsGrew: false, watermarkGrew: false);

            StringAssert.Contains("fill 0ms", row);
        }

        [Test]
        public void PaintGrowingUnderrunsRed()
        {
            AudioStats stats = StatsAt48KStereo();
            stats.JitterUnderruns = 3;

            string calm = MediaPlayerAudioDebugFormatter.JitterRow(stats, isPlaying: false, underrunsGrew: false, watermarkGrew: false);
            string growing = MediaPlayerAudioDebugFormatter.JitterRow(stats, isPlaying: false, underrunsGrew: true, watermarkGrew: false);

            StringAssert.Contains("underruns 3", calm);
            StringAssert.DoesNotContain("<color=red>underruns 3</color>", calm);
            StringAssert.Contains("<color=red>underruns 3</color>", growing);
        }

        [Test]
        public void PaintUnprimedRedOnlyWhilePlaying()
        {
            AudioStats stats = StatsAt48KStereo(); // default = unprimed

            string playing = MediaPlayerAudioDebugFormatter.JitterRow(stats, isPlaying: true, underrunsGrew: false, watermarkGrew: false);
            string paused = MediaPlayerAudioDebugFormatter.JitterRow(stats, isPlaying: false, underrunsGrew: false, watermarkGrew: false);

            StringAssert.Contains("<color=red>primed:n</color>", playing);
            StringAssert.DoesNotContain("<color=red>", paused);
        }

        [Test]
        public void PaintGrowingDriftDropsRed()
        {
            AudioStats stats = StatsAt48KStereo();
            stats.CoreDriftDroppedSamples = 9_600; // 100 ms

            string row = MediaPlayerAudioDebugFormatter.CoreRow(stats, driftGrew: true);

            StringAssert.Contains("<color=red>drift-drop 100ms</color>", row);
        }

        [Test]
        public void FlagZeroNegotiatedChannelsAsMute()
        {
            string row = MediaPlayerAudioDebugFormatter.DspRow(Player(nativeChannels: 0));

            StringAssert.Contains("<color=red>ch 0", row);
        }

        [Test]
        public void CompactLargeFrameCounts()
        {
            string row = MediaPlayerAudioDebugFormatter.DspRow(Player(nativeChannels: 2, requested: 4_200_000, returned: 12_300, silenced: 7));

            StringAssert.Contains("req 4.20M", row);
            StringAssert.Contains("ret 12.3k", row);
            StringAssert.Contains("mute 7", row);
        }

        [Test]
        public void ReportUnavailableEngineStats()
        {
            string row = MediaPlayerAudioDebugFormatter.EngineRow(default, available: false, clampsGrew: false);

            StringAssert.Contains("n/a", row);
        }

        [Test]
        public void PaintSlowServeIterationsRed()
        {
            var calm = new EngineAudioStats { ServeMaxIterUs = 12_400 };
            var slow = new EngineAudioStats { ServeMaxIterUs = 150_000 };

            string calmRow = MediaPlayerAudioDebugFormatter.EngineRow(calm, available: true, clampsGrew: false);
            string slowRow = MediaPlayerAudioDebugFormatter.EngineRow(slow, available: true, clampsGrew: false);

            StringAssert.Contains("serve max 12.4ms", calmRow);
            StringAssert.DoesNotContain("<color=red>", calmRow);
            StringAssert.Contains("<color=red>serve max 150.0ms</color>", slowRow);
        }

        [Test]
        public void PaintGrowingPullClampsRed()
        {
            var stats = new EngineAudioStats { AudioPullClamps = 4 };

            string row = MediaPlayerAudioDebugFormatter.EngineRow(stats, available: true, clampsGrew: true);

            StringAssert.Contains("<color=red>pull clamps 4</color>", row);
        }
    }
}
