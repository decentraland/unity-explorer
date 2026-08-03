using NUnit.Framework;

namespace UUAV.SmokeTests
{
    public class AudioClockShould
    {
        [Test]
        public void MediaTimeDerivation()
        {
            Assert.AreEqual(100.0, AudioMasterClock.MediaTime(100.0, 1_000, 1_000, 1.0, 48_000), 1e-9,
                "at the basis the clock reads the basis pts");
            Assert.AreEqual(100.1, AudioMasterClock.MediaTime(100.0, 1_000 + 4_800, 1_000, 1.0, 48_000), 1e-9,
                "consumed frames advance the clock at the sample rate");
            Assert.AreEqual(100.2, AudioMasterClock.MediaTime(100.0, 1_000 + 4_800, 1_000, 2.0, 48_000), 1e-9,
                "varispeed scales consumed frames into media seconds");
            Assert.AreEqual(100.0, AudioMasterClock.MediaTime(100.0, 500, 1_000, 1.0, 48_000), 1e-9,
                "a count from before the basis clamps instead of running backwards");
            Assert.AreEqual(100.0, AudioMasterClock.MediaTime(100.0, 9_999, 0, 1.0, 0), 1e-9,
                "a non-positive sample rate holds the clock at the basis");

            const int sampleRate = 48_000;
            long consumed = 0;

            long baseFramesA = consumed;
            for (var i = 0; i < 47; i++) consumed += 1_024;
            Assert.AreEqual(47 * 1_024 / (double)sampleRate,
                AudioMasterClock.MediaTime(0.0, consumed, baseFramesA, 1.0, sampleRate), 1e-9,
                "before the seek the clock tracks stream A");

            long baseFramesB = consumed;
            for (var i = 0; i < 24; i++) consumed += 1_024;
            Assert.AreEqual(100.0 + 24 * 1_024 / (double)sampleRate,
                AudioMasterClock.MediaTime(100.0, consumed, baseFramesB, 1.0, sampleRate), 1e-9,
                "after the rebase the clock continues from the new stream's pts");
        }
    }
}
