using DCL.Web3;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Pulse
{
    /// <summary>
    /// Retains overlapping peer registrations and selects one peer per wallet for routing. Public access is serialized by
    /// <c>sync</c>. Callbacks run inside that lock and must not acquire locks in reverse order to callers of this cache.
    /// </summary>
    public class PeerIdCache
    {
        public enum LookupResult : byte
        {
            Found,
            UnknownPeer,
            RealmMismatch,
            SupersededPeer,
        }

        private readonly object sync = new ();
        private readonly Dictionary<uint, (Web3Address wallet, string realm, ulong joinOrder)> peersByWallet = new ();
        private readonly Dictionary<Web3Address, uint> walletsByPeerId = new ();
        private readonly List<uint> removalBuffer = new ();
        private readonly HashSet<Web3Address> walletsBuffer = new ();
        private ulong joinOrder;

        /// <summary>Registers a peer or refreshes its realm without promoting an existing shadow. Peer ID reuse returns an orphaned previous wallet or reports its promoted survivor.</summary>
        public Web3Address? Set(Web3Address wallet, uint peerId, string realm, out uint? promotedPeerId)
        {
            lock (sync)
            {
                Web3Address? orphanedWallet = null;
                promotedPeerId = null;

                if (peersByWallet.TryGetValue(peerId, out var previousEntry))
                {
                    if (previousEntry.wallet.Equals(wallet))
                    {
                        peersByWallet[peerId] = (wallet, realm, previousEntry.joinOrder);
                        return null;
                    }

                    if (RemovePeer(peerId, null, out Web3Address previousWallet, out promotedPeerId))
                        orphanedWallet = previousWallet;
                }

                peersByWallet[peerId] = (wallet, realm, ++joinOrder);
                walletsByPeerId[wallet] = peerId;
                return orphanedWallet;
            }
        }

        /// <summary>Removes a registration, selecting the newest eligible survivor. Returns whether the wallet lost its last registration in the realm.</summary>
        public bool Remove(uint peerId, string realm, out Web3Address wallet, out uint? promotedPeerId)
        {
            lock (sync)
            {
                return RemovePeer(peerId, realm, out wallet, out promotedPeerId);
            }
        }

        /// <summary>Invokes the callback once per wallet, then clears every registration.</summary>
        public void RemoveAll(Action<string> onWalletRemoved)
        {
            lock (sync)
            {
                foreach (Web3Address wallet in walletsByPeerId.Keys)
                    onWalletRemoved(wallet);

                peersByWallet.Clear();
                walletsByPeerId.Clear();
            }
        }

        public bool TryGetWallet(uint peerId, out Web3Address wallet)
        {
            lock (sync)
            {
                if (peersByWallet.TryGetValue(peerId, out var entry))
                {
                    wallet = entry.wallet;
                    return true;
                }

                wallet = default(Web3Address);
                return false;
            }
        }

        public LookupResult GetWalletInRealm(uint peerId, string realm, out Web3Address wallet)
        {
            lock (sync)
            {
                wallet = default(Web3Address);

                if (!peersByWallet.TryGetValue(peerId, out var entry))
                    return LookupResult.UnknownPeer;

                if (entry.realm != realm)
                    return LookupResult.RealmMismatch;

                if (walletsByPeerId[entry.wallet] != peerId)
                    return LookupResult.SupersededPeer;

                wallet = entry.wallet;
                return LookupResult.Found;
            }
        }

        public bool TryGetPeerId(Web3Address wallet, out uint peerId)
        {
            lock (sync)
            {
                return walletsByPeerId.TryGetValue(wallet, out peerId);
            }
        }

        public void CollectWalletsNotInRealm(string realm, ICollection<string> result)
        {
            lock (sync)
            {
                walletsBuffer.Clear();

                foreach (Web3Address wallet in walletsByPeerId.Keys)
                    walletsBuffer.Add(wallet);

                foreach (var entry in peersByWallet.Values)
                    if (entry.realm == realm)
                        walletsBuffer.Remove(entry.wallet);

                foreach (Web3Address wallet in walletsBuffer)
                    result.Add(wallet);
            }
        }

        /// <summary>Removes stale-realm registrations and repairs selection using survivors in the requested realm.</summary>
        public void RemoveWhereNotInRealm(string realm, Action<uint> onPeerRemoved, Action<Web3Address, uint> onPeerPromoted)
        {
            lock (sync)
            {
                removalBuffer.Clear();

                foreach (var pair in peersByWallet)
                    if (pair.Value.realm != realm)
                        removalBuffer.Add(pair.Key);

                foreach (uint peerId in removalBuffer)
                {
                    RemovePeer(peerId, realm, out Web3Address wallet, out uint? promotedPeerId);
                    onPeerRemoved(peerId);

                    if (promotedPeerId.HasValue)
                        onPeerPromoted(wallet, promotedPeerId.Value);
                }
            }
        }

        private bool RemovePeer(uint peerId, string? realm, out Web3Address wallet, out uint? promotedPeerId)
        {
            wallet = default(Web3Address);
            promotedPeerId = null;

            if (!peersByWallet.Remove(peerId, out var entry))
                return false;

            wallet = entry.wallet;
            bool wasSelected = walletsByPeerId[wallet] == peerId;

            if (!wasSelected && (realm == null || peersByWallet[walletsByPeerId[wallet]].realm == realm))
                return false;

            bool hasEligiblePeer = TrySelectPeer(wallet, realm, out uint survivor);

            if (hasEligiblePeer)
            {
                if (wasSelected || (realm != null && peersByWallet[walletsByPeerId[wallet]].realm != realm))
                {
                    walletsByPeerId[wallet] = survivor;
                    promotedPeerId = survivor;
                }

                return false;
            }

            if (TrySelectPeer(wallet, null, out survivor))
                walletsByPeerId[wallet] = survivor;
            else
                walletsByPeerId.Remove(wallet);

            return true;
        }

        private bool TrySelectPeer(Web3Address wallet, string? realm, out uint peerId)
        {
            peerId = 0;
            ulong latestJoinOrder = 0;
            var found = false;

            foreach (var pair in peersByWallet)
            {
                var entry = pair.Value;

                if (!entry.wallet.Equals(wallet) || (realm != null && entry.realm != realm) || entry.joinOrder <= latestJoinOrder)
                    continue;

                latestJoinOrder = entry.joinOrder;
                peerId = pair.Key;
                found = true;
            }

            return found;
        }
    }
}
