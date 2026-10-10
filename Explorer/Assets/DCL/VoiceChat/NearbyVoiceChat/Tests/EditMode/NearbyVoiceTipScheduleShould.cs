using DCL.VoiceChat.UI;
using NUnit.Framework;

namespace DCL.VoiceChat.NearbyVoiceChat.Tests.EditMode
{
    /// <summary>
    ///     Documents when the Nearby Voice Chat intro tip is due.
    ///
    ///     With the shipped defaults (every 5 sessions, at most 2 displays) a fresh user sees the tip on launch 5 and
    ///     again on launch 10. The gap is measured from the last display, not from launch 0, so a returning user who
    ///     is already past every threshold gets one display now and the next one 5 launches later — never two in a row.
    /// </summary>
    public class NearbyVoiceTipScheduleShould
    {
        private const int SHOW_EVERY_SESSIONS = 5;
        private const int MAX_TIMES_SHOWN = 2;
        private const int NEVER_SHOWN = 0;

        private NearbyVoiceTipSchedule schedule;

        [SetUp]
        public void Setup()
        {
            schedule = new NearbyVoiceTipSchedule(SHOW_EVERY_SESSIONS, MAX_TIMES_SHOWN);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void NotShowBeforeTheFirstThreshold(int launchCount)
        {
            //Act
            bool shouldShow = schedule.ShouldShow(launchCount, timesShown: 0, lastShownAtLaunch: NEVER_SHOWN, hasUsedNearbyVoice: false);

            //Assert
            Assert.IsFalse(shouldShow);
        }

        [Test]
        public void ShowAtTheFirstThreshold()
        {
            //Act
            bool shouldShow = schedule.ShouldShow(launchCount: 5, timesShown: 0, lastShownAtLaunch: NEVER_SHOWN, hasUsedNearbyVoice: false);

            //Assert
            Assert.IsTrue(shouldShow);
        }

        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        public void NotShowBetweenThresholds(int launchCount)
        {
            //Act
            bool shouldShow = schedule.ShouldShow(launchCount, timesShown: 1, lastShownAtLaunch: 5, hasUsedNearbyVoice: false);

            //Assert
            Assert.IsFalse(shouldShow);
        }

        [Test]
        public void ShowAtTheSecondThreshold()
        {
            //Act
            bool shouldShow = schedule.ShouldShow(launchCount: 10, timesShown: 1, lastShownAtLaunch: 5, hasUsedNearbyVoice: false);

            //Assert
            Assert.IsTrue(shouldShow);
        }

        [TestCase(10)]
        [TestCase(11)]
        [TestCase(100)]
        public void NotShowOnceTheDisplayCapIsReached(int launchCount)
        {
            //Act
            bool shouldShow = schedule.ShouldShow(launchCount, timesShown: MAX_TIMES_SHOWN, lastShownAtLaunch: 10, hasUsedNearbyVoice: false);

            //Assert
            Assert.IsFalse(shouldShow);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void NotShowWhenTheUserAlreadyUsedNearbyVoice(int timesShown)
        {
            //Act
            bool shouldShow = schedule.ShouldShow(launchCount: 100, timesShown, lastShownAtLaunch: NEVER_SHOWN, hasUsedNearbyVoice: true);

            //Assert
            Assert.IsFalse(shouldShow);
        }

        [Test]
        public void ShowImmediatelyToReturningUsersWhoNeverSawIt()
        {
            //Act
            bool shouldShow = schedule.ShouldShow(launchCount: 42, timesShown: 0, lastShownAtLaunch: NEVER_SHOWN, hasUsedNearbyVoice: false);

            //Assert
            Assert.IsTrue(shouldShow);
        }

        [TestCase(43)]
        [TestCase(44)]
        [TestCase(46)]
        public void NotShowAgainRightAfterAReturningUsersFirstDisplay(int launchCount)
        {
            //Arrange
            const int SHOWN_AT = 42;

            //Act
            bool shouldShow = schedule.ShouldShow(launchCount, timesShown: 1, lastShownAtLaunch: SHOWN_AT, hasUsedNearbyVoice: false);

            //Assert
            Assert.IsFalse(shouldShow);
        }

        [Test]
        public void ShowTheSecondTimeAFullPeriodAfterAReturningUsersFirstDisplay()
        {
            //Act
            bool shouldShow = schedule.ShouldShow(launchCount: 47, timesShown: 1, lastShownAtLaunch: 42, hasUsedNearbyVoice: false);

            //Assert
            Assert.IsTrue(shouldShow);
        }

        [TestCase(3, 0, 0, ExpectedResult = true)]
        [TestCase(5, 1, 3, ExpectedResult = false)]
        [TestCase(6, 1, 3, ExpectedResult = true)]
        [TestCase(12, 3, 9, ExpectedResult = true)]
        [TestCase(12, 4, 9, ExpectedResult = false)]
        public bool FollowTheConfiguredFrequency(int launchCount, int timesShown, int lastShownAtLaunch)
        {
            //Arrange
            var customSchedule = new NearbyVoiceTipSchedule(showEverySessions: 3, maxTimesShown: 4);

            //Act
            return customSchedule.ShouldShow(launchCount, timesShown, lastShownAtLaunch, hasUsedNearbyVoice: false);
        }

        [Test]
        public void NeverShowWhenDisabled()
        {
            //Act
            bool shouldShow = NearbyVoiceTipSchedule.Disabled.ShouldShow(launchCount: 100, timesShown: 0, lastShownAtLaunch: NEVER_SHOWN, hasUsedNearbyVoice: false);

            //Assert
            Assert.IsFalse(shouldShow);
        }

        [Test]
        public void ClampANonPositivePeriodSoTheTipIsNotDueEveryLaunch()
        {
            //Arrange
            var degenerateSchedule = new NearbyVoiceTipSchedule(showEverySessions: 0, maxTimesShown: 2);

            //Act
            bool shouldShow = degenerateSchedule.ShouldShow(launchCount: 5, timesShown: 1, lastShownAtLaunch: 5, hasUsedNearbyVoice: false);

            //Assert
            Assert.AreEqual(1, degenerateSchedule.ShowEverySessions);
            Assert.IsFalse(shouldShow);
        }
    }
}