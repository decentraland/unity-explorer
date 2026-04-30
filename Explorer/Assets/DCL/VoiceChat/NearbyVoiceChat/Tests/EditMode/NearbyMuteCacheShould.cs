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
            Assert.That(cache.IsMuted("0xabc"), Is.False);
        }

        [Test]
        public void TrackMutedUser()
        {
            cache.SetMuted("0xabc", true);

            Assert.That(cache.IsMuted("0xabc"), Is.True);
        }

        [Test]
        public void StopTrackingAfterUnmute()
        {
            cache.SetMuted("0xabc", true);

            cache.SetMuted("0xabc", false);

            Assert.That(cache.IsMuted("0xabc"), Is.False);
        }

        [Test]
        public void FireEventOnMute()
        {
            cache.SetMuted("0xabc", true);

            Assert.That(receivedEvents.Count, Is.EqualTo(1));
            Assert.That(receivedEvents[0].walletId, Is.EqualTo("0xabc"));
            Assert.That(receivedEvents[0].muted, Is.True);
        }

        [Test]
        public void FireEventOnUnmute()
        {
            cache.SetMuted("0xabc", true);
            receivedEvents.Clear();

            cache.SetMuted("0xabc", false);

            Assert.That(receivedEvents.Count, Is.EqualTo(1));
            Assert.That(receivedEvents[0].muted, Is.False);
        }

        [Test]
        public void NotFireEventWhenAlreadyMuted()
        {
            cache.SetMuted("0xabc", true);
            receivedEvents.Clear();

            cache.SetMuted("0xabc", true);

            Assert.That(receivedEvents, Is.Empty);
        }

        [Test]
        public void NotFireEventWhenAlreadyUnmuted()
        {
            cache.SetMuted("0xabc", false);

            Assert.That(receivedEvents, Is.Empty);
        }

        [Test]
        public void AddServerEntriesOnMerge()
        {
            cache.Merge(new[] { "0xnew1", "0xnew2" });

            Assert.That(cache.IsMuted("0xnew1"), Is.True);
            Assert.That(cache.IsMuted("0xnew2"), Is.True);
        }

        [Test]
        public void PreserveLocalMutesOnMerge()
        {
            cache.SetMuted("0xlocal", true);
            receivedEvents.Clear();

            cache.Merge(new[] { "0xserver" });

            Assert.That(cache.IsMuted("0xlocal"), Is.True);
            Assert.That(cache.IsMuted("0xserver"), Is.True);
        }

        [Test]
        public void FireMutedEventsOnlyForNewEntriesOnMerge()
        {
            cache.SetMuted("0xlocal", true);
            receivedEvents.Clear();

            cache.Merge(new[] { "0xlocal", "0xserver" });

            Assert.That(receivedEvents, Is.EquivalentTo(new[]
            {
                ("0xserver", true),
            }));
        }

        [Test]
        public void NotFireEventsWhenAllServerEntriesAlreadyMutedLocally()
        {
            cache.SetMuted("0xa", true);
            cache.SetMuted("0xb", true);
            receivedEvents.Clear();

            cache.Merge(new[] { "0xa", "0xb" });

            Assert.That(receivedEvents, Is.Empty);
        }

        [Test]
        public void NotReMuteAfterLocalUnmuteOnMerge()
        {
            // Edge case: user unmutes X before LoadAsync returns, server snapshot still has X.
            cache.SetMuted("0xx", false);

            cache.Merge(new[] { "0xx" });

            Assert.That(cache.IsMuted("0xx"), Is.False);
        }

        [Test]
        public void NotFireEventForLocallyUnmutedAddressOnMerge()
        {
            cache.SetMuted("0xx", false);
            receivedEvents.Clear();

            cache.Merge(new[] { "0xx" });

            Assert.That(receivedEvents, Is.Empty);
        }

        [Test]
        public void ReMuteAfterUnmuteThenReMuteBeforeMerge()
        {
            // User flips the decision: unmute, then mute again, before LoadAsync returns.
            cache.SetMuted("0xx", false);
            cache.SetMuted("0xx", true);
            receivedEvents.Clear();

            cache.Merge(new[] { "0xx" });

            // Already muted locally after the re-mute — Merge is a no-op for this address.
            Assert.That(cache.IsMuted("0xx"), Is.True);
            Assert.That(receivedEvents, Is.Empty);
        }

        [Test]
        public void NormalizeAddressOnWrite()
        {
            // Write side normalizes, read side trusts lowercase input. Mixed-case writes flow into the lowercase bucket.
            cache.SetMuted("0xAbC", true);

            Assert.That(cache.IsMuted("0xabc"), Is.True);
        }

        [Test]
        public void NormalizeAddressOnMerge()
        {
            // Defends against the social service ever returning EIP-55 checksummed addresses.
            cache.Merge(new[] { "0xCheckSummed" });

            Assert.That(cache.IsMuted("0xchecksummed"), Is.True);
        }

        [Test]
        public void StartVersionAtOne()
        {
            // Component diff-state init (LastSeenMuteVersion=0) relies on this for first-tick recompute.
            Assert.That(cache.Version, Is.EqualTo(1u));
        }

        [Test]
        public void IncrementVersionOnSetMutedTrueWhenAddressIsNew()
        {
            uint before = cache.Version;

            cache.SetMuted("0xabc", true);

            Assert.That(cache.Version, Is.EqualTo(before + 1));
        }

        [Test]
        public void NotIncrementVersionWhenSettingAlreadyMutedAddress()
        {
            cache.SetMuted("0xabc", true);
            uint before = cache.Version;

            cache.SetMuted("0xabc", true);

            Assert.That(cache.Version, Is.EqualTo(before));
        }

        [Test]
        public void IncrementVersionOnUnmuteOfMutedAddress()
        {
            cache.SetMuted("0xabc", true);
            uint before = cache.Version;

            cache.SetMuted("0xabc", false);

            Assert.That(cache.Version, Is.EqualTo(before + 1));
        }

        [Test]
        public void NotIncrementVersionWhenUnmutingNonMutedAddress()
        {
            uint before = cache.Version;

            cache.SetMuted("0xabc", false);

            Assert.That(cache.Version, Is.EqualTo(before));
        }

        [Test]
        public void IncrementVersionOnMergeForNewAddressesOnly()
        {
            cache.SetMuted("0xa", true);
            uint before = cache.Version;

            cache.Merge(new[] { "0xa", "0xb" });

            // Only "0xb" is new — version moves by exactly one.
            Assert.That(cache.Version, Is.EqualTo(before + 1));
        }
    }
}
