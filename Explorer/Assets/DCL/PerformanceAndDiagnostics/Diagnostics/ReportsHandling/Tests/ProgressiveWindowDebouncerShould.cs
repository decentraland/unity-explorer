using NUnit.Framework;
using System;
using UnityEngine;

namespace DCL.Diagnostics.Tests
{
    [TestFixture]
    public class ProgressiveWindowDebouncerTests
    {
        [SetUp]
        public void SetUp()
        {
            fakeNow = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            timeProvider = () => fakeNow;

            debouncer = new ProgressiveWindowDebouncer(
                initialWindow,
                maxWindow,
                cleanupInterval,
                timeProvider,
                backoffFactor,
                allowedReps);
        }

        private readonly TimeSpan initialWindow = TimeSpan.FromSeconds(10);
        private readonly TimeSpan maxWindow = TimeSpan.FromMinutes(30);
        private readonly TimeSpan cleanupInterval = TimeSpan.FromSeconds(30);
        private readonly double backoffFactor = 1.7;
        private readonly byte allowedReps = 3;
        private DateTime fakeNow;
        private Func<DateTime> timeProvider;
        private ProgressiveWindowDebouncer debouncer;

        private Exception CreateException(string message) =>
            new (message);

        [Test]
        public void FirstNRepetitions_NotDebounced([NUnit.Framework.Range(1, 3)] int occurrence)
        {
            Exception ex = CreateException("test");
            var result = false;

            for (var i = 0; i < occurrence; i++)
                result = debouncer.Debounce(ex, null, default(LogType));

            Assert.IsFalse(result, $"Occurrence {occurrence} of exception should not be debounced");
        }

        [Test]
        public void NextOccurrenceWithinInitialWindow_Debounced()
        {
            Exception ex = CreateException("inner");

            for (var i = 0; i < allowedReps; i++)
                debouncer.Debounce(ex, null, default(LogType));

            // No time advance => next should be debounced under initial window
            bool result = debouncer.Debounce(ex, null, default(LogType));
            Assert.IsTrue(result, "Exception after allowed within initial window should be debounced");
        }

        [Test]
        [TestCase(10)]
        [TestCase(15)]
        public void NextOccurrenceAfterInitialWindow_NotDebounced(int offsetSeconds)
        {
            Exception ex = CreateException("later");

            for (var i = 0; i < allowedReps; i++)
                debouncer.Debounce(ex, null, default(LogType));

            fakeNow = fakeNow.AddSeconds(offsetSeconds);
            bool result = debouncer.Debounce(ex, null, default(LogType));
            Assert.IsFalse(result, "Exception after initial window should not be debounced");
        }

        [Test]
        [TestCase(4, 17.0)] // Attempt 4: 10s * 1.7^(4-3) = 17s
        [TestCase(5, 28.9)] // Attempt 5: 10s * 1.7^(5-3) ≈ 28.9s
        [TestCase(6, 49.13)] // Attempt 6: 10s * 1.7^(6-3) ≈ 49.13s
        public void ExponentialBackoff_WindowAndDebounce(int attemptNumber, double expectedWindow)
        {
            Exception ex = CreateException("dynamic");

            // Burn initial allowed repetitions
            for (var i = 0; i < allowedReps; i++)
                debouncer.Debounce(ex, null, default(LogType));

            // Fast retry for attemptNumber within window: should be debounced
            fakeNow = fakeNow.AddSeconds(0); // no advance
            var debounced = false;

            for (int i = allowedReps + 1; i <= attemptNumber; i++)
                debounced = debouncer.Debounce(ex, null, default(LogType));

            Assert.IsTrue(debounced, $"Attempt {attemptNumber} within expected window {expectedWindow}s should be debounced");

            // Move beyond window
            fakeNow = fakeNow.AddSeconds(expectedWindow + 1);
            bool allowed = debouncer.Debounce(ex, null, default(LogType));
            Assert.IsFalse(allowed, $"Attempt after expected window {expectedWindow}s should not be debounced");
        }

        [Test]
        [TestCase(1, 30.1)] // Exceeding the initial window * 3
        [TestCase(1, 50)]
        [TestCase(2, 59)] // > 29 * 2
        [TestCase(3, 148)] // > 49.13 * 3
        public void CleanUp_ResetsTrackerAfterIdle(int retriesCount, double idleTime)
        {
            // Idle time should cover: cleanUpInterval, initialWindow * allowedReps and dynamicWindow * retriesCount

            Exception ex = CreateException("cleanup");

            for (var i = 0; i < allowedReps + retriesCount; i++)
                debouncer.Debounce(ex, ReportData.UNSPECIFIED, default(LogType));

            fakeNow = fakeNow.AddSeconds(idleTime);

            // After this point, the tracker should be cleaned up
            Assert.IsTrue(debouncer.TryCleanUp(fakeNow));

            Assert.That(debouncer.Exceptions.Count, Is.EqualTo(0), "After cleanup, no exceptions should be tracked");
        }

        [Test]
        [TestCase(2, 40)] // < 29 * 2
        [TestCase(3, 147)] // > 49.13 * 3
        [TestCase(3, 110)] // > 49.13 * 3
        public void CleanUp_KeepTracker(int retriesCount, double idleTime)
        {
            // Idle time should cover: cleanUpInterval, initialWindow * allowedReps and dynamicWindow * retriesCount

            Exception ex = CreateException("cleanup");

            for (var i = 0; i < allowedReps + retriesCount; i++)
                debouncer.Debounce(ex, ReportData.UNSPECIFIED, default(LogType));

            fakeNow = fakeNow.AddSeconds(idleTime);

            // After this point, the tracker should be cleaned up
            Assert.IsTrue(debouncer.TryCleanUp(fakeNow)); // enough time passed to trigger cleanup

            Assert.That(debouncer.Exceptions.Count, Is.EqualTo(1), "After cleanup, one exception should still be tracked");
        }

        [Test]
        public void GradualRampDown_LowersBackoffWindow()
        {
            Exception ex = CreateException("rampdown2");
            var key = new ExceptionFingerprint(ex);

            // Burn through some retries to increase window
            const int EXTRA_RETRIES = 2;

            for (var i = 0; i < allowedReps + EXTRA_RETRIES; i++)
                debouncer.Debounce(ex, ReportData.UNSPECIFIED, default(LogType));

            // At this point the window is 10s * 1.7^(5-3) ≈ 28.9s
            Assert.IsTrue(debouncer.Exceptions.TryGetValue(key, out ProgressiveWindowDebouncer.Tracker tracker));

            // The stored value will correspond to the previous report: 17s
            Assert.AreEqual(17, tracker.Window.TotalSeconds, 0.5);

            // Decay more than the current window to trigger ramp down
            var cooldownWindow = TimeSpan.FromSeconds(30);
            fakeNow = fakeNow.Add(cooldownWindow);

            // Now it should decay to the previous window: 10s * 1.7^(4-3) = 17s
            // But debouncing it again will bring it back to 28.9s window
            bool debounced = debouncer.Debounce(ex, ReportData.UNSPECIFIED, default(LogType));
            Assert.That(debounced, Is.False);

            // Add less than a window
            fakeNow = fakeNow.AddSeconds(26);

            debounced = debouncer.Debounce(ex, ReportData.UNSPECIFIED, default(LogType)); // must be debounced

            // The window will be brought to 10s * 1.7^(6-3) ≈ 49.13s
            Assert.That(debounced, Is.True, "After ramp down, the next occurrence should be debounced");

            // Now wait for more than 3 windows to rump down to the initial window
            fakeNow = fakeNow.AddSeconds(50 * 3);
            debouncer.Debounce(ex, ReportData.UNSPECIFIED, default(LogType));

            // Check the window
            Assert.IsTrue(debouncer.Exceptions.TryGetValue(key, out tracker));

            Assert.AreEqual(initialWindow.TotalSeconds, tracker.Window.TotalSeconds, 0.5);
        }
    }
}
