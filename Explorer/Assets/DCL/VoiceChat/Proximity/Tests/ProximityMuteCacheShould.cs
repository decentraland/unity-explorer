using DCL.VoiceChat.MutePersistence;
using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.VoiceChat.Tests
{
    [TestFixture]
    public class ProximityMuteCacheShould
    {
        private ProximityMuteCache cache;
        private List<(string walletId, bool muted)> receivedEvents;

        [SetUp]
        public void SetUp()
        {
            cache = new ProximityMuteCache();
            receivedEvents = new List<(string, bool)>();
            cache.MuteStateChanged += (id, muted) => receivedEvents.Add((id, muted));
        }

        [Test]
        public void ReportNoOneMutedInitially()
        {
            Assert.That(cache.IsMuted("0xABC"), Is.False);
        }

        [Test]
        public void TrackMutedUser()
        {
            // Act
            cache.SetMuted("0xABC", true);

            // Assert
            Assert.That(cache.IsMuted("0xABC"), Is.True);
        }

        [Test]
        public void StopTrackingAfterUnmute()
        {
            // Arrange
            cache.SetMuted("0xABC", true);

            // Act
            cache.SetMuted("0xABC", false);

            // Assert
            Assert.That(cache.IsMuted("0xABC"), Is.False);
        }

        [Test]
        public void FireEventOnMute()
        {
            // Act
            cache.SetMuted("0xABC", true);

            // Assert
            Assert.That(receivedEvents.Count, Is.EqualTo(1));
            Assert.That(receivedEvents[0].walletId, Is.EqualTo("0xABC"));
            Assert.That(receivedEvents[0].muted, Is.True);
        }

        [Test]
        public void FireEventOnUnmute()
        {
            // Arrange
            cache.SetMuted("0xABC", true);
            receivedEvents.Clear();

            // Act
            cache.SetMuted("0xABC", false);

            // Assert
            Assert.That(receivedEvents.Count, Is.EqualTo(1));
            Assert.That(receivedEvents[0].muted, Is.False);
        }

        [Test]
        public void NotFireEventWhenAlreadyMuted()
        {
            // Arrange
            cache.SetMuted("0xABC", true);
            receivedEvents.Clear();

            // Act
            cache.SetMuted("0xABC", true);

            // Assert
            Assert.That(receivedEvents, Is.Empty);
        }

        [Test]
        public void NotFireEventWhenAlreadyUnmuted()
        {
            // Act
            cache.SetMuted("0xABC", false);

            // Assert
            Assert.That(receivedEvents, Is.Empty);
        }

        [Test]
        public void ReplaceAllEntriesOnReset()
        {
            // Arrange
            cache.SetMuted("0xOLD", true);

            // Act
            cache.Reset(new[] { "0xNEW1", "0xNEW2" });

            // Assert
            Assert.That(cache.IsMuted("0xOLD"), Is.False);
            Assert.That(cache.IsMuted("0xNEW1"), Is.True);
            Assert.That(cache.IsMuted("0xNEW2"), Is.True);
        }

        [Test]
        public void MatchAddressesCaseInsensitively()
        {
            // Arrange
            cache.SetMuted("0xAbC", true);

            // Assert
            Assert.That(cache.IsMuted("0xabc"), Is.True);
            Assert.That(cache.IsMuted("0xABC"), Is.True);
        }
    }
}
