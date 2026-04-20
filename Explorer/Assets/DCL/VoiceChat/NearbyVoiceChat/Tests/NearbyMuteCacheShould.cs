using DCL.VoiceChat.Nearby.MutePersistence;
using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.VoiceChat.Nearby.Tests
{
    [TestFixture]
    public class NearbyMuteCacheShould
    {
        private NearbyMuteCache cache;
        private List<(string walletId, bool muted)> receivedEvents;

        [SetUp]
        public void SetUp()
        {
            cache = new NearbyMuteCache();
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
            cache.SetMuted("0xABC", true);

            Assert.That(cache.IsMuted("0xABC"), Is.True);
        }

        [Test]
        public void StopTrackingAfterUnmute()
        {
            cache.SetMuted("0xABC", true);

            cache.SetMuted("0xABC", false);

            Assert.That(cache.IsMuted("0xABC"), Is.False);
        }

        [Test]
        public void FireEventOnMute()
        {
            cache.SetMuted("0xABC", true);

            Assert.That(receivedEvents.Count, Is.EqualTo(1));
            Assert.That(receivedEvents[0].walletId, Is.EqualTo("0xABC"));
            Assert.That(receivedEvents[0].muted, Is.True);
        }

        [Test]
        public void FireEventOnUnmute()
        {
            cache.SetMuted("0xABC", true);
            receivedEvents.Clear();

            cache.SetMuted("0xABC", false);

            Assert.That(receivedEvents.Count, Is.EqualTo(1));
            Assert.That(receivedEvents[0].muted, Is.False);
        }

        [Test]
        public void NotFireEventWhenAlreadyMuted()
        {
            cache.SetMuted("0xABC", true);
            receivedEvents.Clear();

            cache.SetMuted("0xABC", true);

            Assert.That(receivedEvents, Is.Empty);
        }

        [Test]
        public void NotFireEventWhenAlreadyUnmuted()
        {
            cache.SetMuted("0xABC", false);

            Assert.That(receivedEvents, Is.Empty);
        }

        [Test]
        public void ReplaceAllEntriesOnReset()
        {
            cache.SetMuted("0xOLD", true);

            cache.Reset(new[] { "0xNEW1", "0xNEW2" });

            Assert.That(cache.IsMuted("0xOLD"), Is.False);
            Assert.That(cache.IsMuted("0xNEW1"), Is.True);
            Assert.That(cache.IsMuted("0xNEW2"), Is.True);
        }

        [Test]
        public void MatchAddressesCaseInsensitively()
        {
            cache.SetMuted("0xAbC", true);

            Assert.That(cache.IsMuted("0xabc"), Is.True);
            Assert.That(cache.IsMuted("0xABC"), Is.True);
        }
    }
}
