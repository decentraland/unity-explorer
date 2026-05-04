using DCL.VoiceChat.Nearby.MutePersistence;
using NUnit.Framework;

namespace DCL.VoiceChat.Nearby.Tests
{
    [TestFixture]
    public class NearbyMuteCacheShould
    {
        private NearbyMuteCache cache;

        [SetUp]
        public void SetUp()
        {
            cache = new NearbyMuteCache();
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

            cache.Merge(new[] { "0xserver" });

            Assert.That(cache.IsMuted("0xlocal"), Is.True);
            Assert.That(cache.IsMuted("0xserver"), Is.True);
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
        public void ReMuteAfterUnmuteThenReMuteBeforeMerge()
        {
            // User flips the decision: unmute, then mute again, before LoadAsync returns.
            cache.SetMuted("0xx", false);
            cache.SetMuted("0xx", true);

            cache.Merge(new[] { "0xx" });

            // Already muted locally after the re-mute — Merge is a no-op for this address.
            Assert.That(cache.IsMuted("0xx"), Is.True);
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
