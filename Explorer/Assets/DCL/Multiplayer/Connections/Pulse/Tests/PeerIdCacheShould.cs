using DCL.Web3;
using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Pulse.Tests
{
    public class PeerIdCacheShould
    {
        private const string WALLET_A = "0x000000000000000000000000000000000000000a";
        private const string WALLET_B = "0x000000000000000000000000000000000000000b";

        [Test]
        public void RetainCaseInsensitiveAliasesWhileRoutingTheLatestJoin()
        {
            var cache = new PeerIdCache();
            cache.Set(new Web3Address(WALLET_A), 7, "realm");
            cache.Set(new Web3Address(WALLET_A.ToUpperInvariant()), 8, "realm");

            Assert.That(cache.TryGetWallet(7, out Web3Address wallet), Is.True);
            Assert.That(wallet.Equals(WALLET_A), Is.True);
            Assert.That(cache.GetWalletInRealm(7, "realm", out _), Is.EqualTo(PeerIdCache.LookupResult.SupersededPeer));
            Assert.That(cache.TryGetPeerId(new Web3Address(WALLET_A), out uint peerId), Is.True);
            Assert.That(peerId, Is.EqualTo(8));
        }

        [TestCase(7u, 8u, 7u)]
        [TestCase(7u, 8u, 8u)]
        [TestCase(8u, 7u, 7u)]
        [TestCase(8u, 7u, 8u)]
        public void ReleaseWalletOnlyAfterBothRegistrationsLeave(uint firstJoin, uint secondJoin, uint firstLeave)
        {
            var cache = new PeerIdCache();
            cache.Set(new Web3Address(WALLET_A), firstJoin, "realm");
            cache.Set(new Web3Address(WALLET_A), secondJoin, "realm");
            uint survivor = firstLeave == 7 ? 8u : 7u;

            Assert.That(cache.Remove(firstLeave, "realm", out Web3Address wallet, out uint? promoted), Is.False);
            Assert.That(wallet.Equals(WALLET_A), Is.True);
            Assert.That(promoted, Is.EqualTo(firstLeave == secondJoin ? survivor : null));
            Assert.That(cache.GetWalletInRealm(survivor, "realm", out _), Is.EqualTo(PeerIdCache.LookupResult.Found));
            Assert.That(cache.TryGetPeerId(new Web3Address(WALLET_A), out uint peerId), Is.True);
            Assert.That(peerId, Is.EqualTo(survivor));

            Assert.That(cache.Remove(survivor, "realm", out wallet, out promoted), Is.True);
            Assert.That(wallet.Equals(WALLET_A), Is.True);
            Assert.That(promoted, Is.Null);
            Assert.That(cache.TryGetPeerId(new Web3Address(WALLET_A), out _), Is.False);
            Assert.That(cache.Remove(firstLeave, "realm", out _, out _), Is.False);
        }

        [Test]
        public void FallBackThroughThreeRegistrationsInOriginalJoinOrder()
        {
            var cache = new PeerIdCache();
            cache.Set(new Web3Address(WALLET_A), 7, "realm");
            cache.Set(new Web3Address(WALLET_A), 8, "realm");
            cache.Set(new Web3Address(WALLET_A), 9, "realm");

            cache.Remove(9, "realm", out _, out _);
            Assert.That(cache.TryGetPeerId(new Web3Address(WALLET_A), out uint peerId), Is.True);
            Assert.That(peerId, Is.EqualTo(8));
            cache.Remove(8, "realm", out _, out _);
            Assert.That(cache.TryGetPeerId(new Web3Address(WALLET_A), out peerId), Is.True);
            Assert.That(peerId, Is.EqualTo(7));
            cache.Remove(7, "realm", out _, out _);
            Assert.That(cache.TryGetPeerId(new Web3Address(WALLET_A), out _), Is.False);
        }

        [Test]
        public void NotPromoteAnAliasWhenItsJoinIsRefreshed()
        {
            var cache = new PeerIdCache();
            cache.Set(new Web3Address(WALLET_A), 7, "realm");
            cache.Set(new Web3Address(WALLET_A), 8, "realm");
            cache.Set(new Web3Address(WALLET_A), 7, "realm");

            Assert.That(cache.TryGetPeerId(new Web3Address(WALLET_A), out uint peerId), Is.True);
            Assert.That(peerId, Is.EqualTo(8));
            Assert.That(cache.GetWalletInRealm(7, "realm", out _), Is.EqualTo(PeerIdCache.LookupResult.SupersededPeer));
        }

        [Test]
        public void ReassignAPeerWithoutLosingThePreviousWalletSurvivor()
        {
            var cache = new PeerIdCache();
            cache.Set(new Web3Address(WALLET_A), 7, "realm");
            cache.Set(new Web3Address(WALLET_A), 8, "realm");
            Assert.That(cache.Set(new Web3Address(WALLET_B), 8, "realm"), Is.Null);

            Assert.That(cache.TryGetPeerId(new Web3Address(WALLET_A), out uint peerId), Is.True);
            Assert.That(peerId, Is.EqualTo(7));
            Assert.That(cache.TryGetPeerId(new Web3Address(WALLET_B), out peerId), Is.True);
            Assert.That(peerId, Is.EqualTo(8));
            Assert.That(cache.TryGetWallet(8, out Web3Address wallet), Is.True);
            Assert.That(wallet.Equals(WALLET_B), Is.True);
        }

        [Test]
        public void RemovePreviousWalletWhenReusingItsOnlyPeerId()
        {
            var cache = new PeerIdCache();
            cache.Set(new Web3Address(WALLET_A), 7, "realm");
            Web3Address? orphanedWallet = cache.Set(new Web3Address(WALLET_B), 7, "realm");

            Assert.That(orphanedWallet.HasValue, Is.True);
            Assert.That(orphanedWallet.GetValueOrDefault().Equals(WALLET_A), Is.True);
            Assert.That(cache.TryGetPeerId(new Web3Address(WALLET_A), out _), Is.False);
            Assert.That(cache.TryGetPeerId(new Web3Address(WALLET_B), out uint peerId), Is.True);
            Assert.That(peerId, Is.EqualTo(7));
        }

        [Test]
        public void PreserveBothDirectionsWhenRefreshingTheSamePeerRealm()
        {
            var cache = new PeerIdCache();
            cache.Set(new Web3Address(WALLET_A), 7, "old-realm");
            cache.Set(new Web3Address(WALLET_A), 7, "new-realm");

            Assert.That(cache.TryGetPeerId(new Web3Address(WALLET_A), out uint peerId), Is.True);
            Assert.That(peerId, Is.EqualTo(7));
            Assert.That(cache.GetWalletInRealm(7, "new-realm", out _), Is.EqualTo(PeerIdCache.LookupResult.Found));
        }

        [Test]
        public void PreserveAnEligibleSiblingWhenPurgingAnotherRealm()
        {
            var cache = new PeerIdCache();
            cache.Set(new Web3Address(WALLET_A), 7, "new-realm");
            cache.Set(new Web3Address(WALLET_A), 8, "old-realm");
            var staleWallets = new List<string>();
            var removedPeers = new List<uint>();

            cache.CollectWalletsNotInRealm("new-realm", staleWallets);
            cache.RemoveWhereNotInRealm("new-realm", removedPeers.Add);

            Assert.That(staleWallets, Is.Empty);
            Assert.That(removedPeers, Is.EquivalentTo(new[] { 8u }));
            Assert.That(cache.TryGetPeerId(new Web3Address(WALLET_A), out uint peerId), Is.True);
            Assert.That(peerId, Is.EqualTo(7));
            Assert.That(cache.GetWalletInRealm(7, "new-realm", out _), Is.EqualTo(PeerIdCache.LookupResult.Found));
        }

        [Test]
        public void NotTreatAnOtherRealmAliasAsAnEligibleSurvivor()
        {
            var cache = new PeerIdCache();
            cache.Set(new Web3Address(WALLET_A), 7, "other-realm");
            cache.Set(new Web3Address(WALLET_A), 8, "realm");

            Assert.That(cache.Remove(8, "realm", out Web3Address wallet, out uint? promoted), Is.True);
            Assert.That(wallet.Equals(WALLET_A), Is.True);
            Assert.That(promoted, Is.Null);
            Assert.That(cache.GetWalletInRealm(7, "realm", out _), Is.EqualTo(PeerIdCache.LookupResult.RealmMismatch));
        }

        [Test]
        public void CollectEachWalletWithNoEligibleSiblingOnce()
        {
            var cache = new PeerIdCache();
            cache.Set(new Web3Address(WALLET_A), 7, "other-realm");
            cache.Set(new Web3Address(WALLET_A), 8, "other-realm");
            var staleWallets = new List<string>();

            cache.CollectWalletsNotInRealm("realm", staleWallets);

            Assert.That(staleWallets, Is.EquivalentTo(new[] { WALLET_A }));
        }

        [Test]
        public void RemoveEachWalletOnceOnDisconnect()
        {
            var cache = new PeerIdCache();
            cache.Set(new Web3Address(WALLET_A), 7, "realm");
            cache.Set(new Web3Address(WALLET_A), 8, "realm");
            cache.Set(new Web3Address(WALLET_B), 9, "realm");
            var removedWallets = new List<string>();

            cache.RemoveAll(removedWallets.Add);

            Assert.That(removedWallets, Is.EquivalentTo(new[] { WALLET_A, WALLET_B }));
            Assert.That(cache.TryGetWallet(7, out _), Is.False);
            Assert.That(cache.TryGetWallet(8, out _), Is.False);
            Assert.That(cache.TryGetWallet(9, out _), Is.False);
            Assert.That(cache.TryGetPeerId(new Web3Address(WALLET_A), out _), Is.False);
            Assert.That(cache.TryGetPeerId(new Web3Address(WALLET_B), out _), Is.False);
        }
    }
}
