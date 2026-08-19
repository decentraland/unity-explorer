using NUnit.Framework;

namespace DCL.BugReporting.Tests
{
    public class PerformanceIssueDetectorShould
    {
        private const float HICCUP_SECONDS = 1f;
        private const float LOW_FPS_THRESHOLD = 20f;
        private const float WINDOW_SECONDS = 10f;

        private PerformanceIssueDetector detector = null!;

        [SetUp]
        public void SetUp() =>
            detector = new PerformanceIssueDetector(HICCUP_SECONDS, LOW_FPS_THRESHOLD, WINDOW_SECONDS);

        [Test]
        public void ReportAHiccupImmediately()
        {
            Assert.IsTrue(detector.OnFrame(2.5f, out PerformanceIssue issue));
            Assert.IsTrue(issue.IsHiccup);
            Assert.AreEqual(2.5f, issue.Value);
        }

        [Test]
        public void ReportSustainedLowFpsOnceTheWindowCompletes()
        {
            var triggered = false;
            var issue = default(PerformanceIssue);

            // 10 FPS frames: the window completes after 100 of them.
            for (var frame = 0; frame < 100 && !triggered; frame++)
                triggered = detector.OnFrame(0.1f, out issue);

            Assert.IsTrue(triggered);
            Assert.IsFalse(issue.IsHiccup);
            Assert.AreEqual(10f, issue.Value, 0.1f);
        }

        [Test]
        public void StaySilentOnAHealthyFrameRate()
        {
            for (var frame = 0; frame < 1200; frame++)
                Assert.IsFalse(detector.OnFrame(1f / 60f, out _));
        }

        [Test]
        public void DiscardTheWindowOnReset()
        {
            for (var frame = 0; frame < 99; frame++)
                detector.OnFrame(0.1f, out _);

            detector.Reset();

            // One more slow frame right after the reset: without the discarded window it proves nothing.
            Assert.IsFalse(detector.OnFrame(0.1f, out _));
        }
    }
}
