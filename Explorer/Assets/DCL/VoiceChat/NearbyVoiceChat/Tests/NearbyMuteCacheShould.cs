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
        public void AddServerEntriesOnMerge()
        {
            cache.Merge(new[] { "0xNEW1", "0xNEW2" });

            Assert.That(cache.IsMuted("0xNEW1"), Is.True);
            Assert.That(cache.IsMuted("0xNEW2"), Is.True);
        }

        [Test]
        public void PreserveLocalMutesOnMerge()
        {
            cache.SetMuted("0xLOCAL", true);
            receivedEvents.Clear();

            cache.Merge(new[] { "0xSERVER" });

            Assert.That(cache.IsMuted("0xLOCAL"), Is.True);
            Assert.That(cache.IsMuted("0xSERVER"), Is.True);
        }

        [Test]
        public void FireMutedEventsOnlyForNewEntriesOnMerge()
        {
            cache.SetMuted("0xLOCAL", true);
            receivedEvents.Clear();

            cache.Merge(new[] { "0xLOCAL", "0xSERVER" });

            Assert.That(receivedEvents, Is.EquivalentTo(new[]
            {
                ("0xSERVER", true),
            }));
        }

        [Test]
        public void NotFireEventsWhenAllServerEntriesAlreadyMutedLocally()
        {
            cache.SetMuted("0xA", true);
            cache.SetMuted("0xB", true);
            receivedEvents.Clear();

            cache.Merge(new[] { "0xA", "0xB" });

            Assert.That(receivedEvents, Is.Empty);
        }

        [Test]
        public void NotReMuteAfterLocalUnmuteOnMerge()
        {
            // Edge case: user unmutes X before LoadAsync returns, server snapshot still has X.
            cache.SetMuted("0xX", false);

            cache.Merge(new[] { "0xX" });

            Assert.That(cache.IsMuted("0xX"), Is.False);
        }

        [Test]
        public void NotFireEventForLocallyUnmutedAddressOnMerge()
        {
            cache.SetMuted("0xX", false);
            receivedEvents.Clear();

            cache.Merge(new[] { "0xX" });

            Assert.That(receivedEvents, Is.Empty);
        }

        [Test]
        public void ReMuteAfterUnmuteThenReMuteBeforeMerge()
        {
            // User flips the decision: unmute, then mute again, before LoadAsync returns.
            cache.SetMuted("0xX", false);
            cache.SetMuted("0xX", true);
            receivedEvents.Clear();

            cache.Merge(new[] { "0xX" });

            // Already muted locally after the re-mute — Merge is a no-op for this address.
            Assert.That(cache.IsMuted("0xX"), Is.True);
            Assert.That(receivedEvents, Is.Empty);
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
