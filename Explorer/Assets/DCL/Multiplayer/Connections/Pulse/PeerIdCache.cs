using DCL.Web3;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Pulse
{
    public class PeerIdCache
    {
        private readonly object sync = new ();
        private readonly Dictionary<uint, (Web3Address wallet, string realm)> peersByWallet = new ();
        private readonly Dictionary<Web3Address, uint> walletsByPeerId = new ();
        private readonly List<uint> removalBuffer = new ();

        public void Set(Web3Address wallet, uint peerId, string realm)
        {
            lock (sync)
            {
                peersByWallet[peerId] = (wallet, realm);
                walletsByPeerId[wallet] = peerId;
            }
        }

        public void Remove(uint peerId)
        {
            lock (sync)
            {
                if (peersByWallet.Remove(peerId, out (Web3Address wallet, string realm) entry))
                    walletsByPeerId.Remove(entry.wallet);
            }
        }

        /// <summary>
        ///     Atomically iterates all wallets, invokes the callback for each, then clears both caches.
        /// </summary>
        public void RemoveAll(Action<string> onWalletRemoved)
        {
            lock (sync)
            {
                foreach ((Web3Address wallet, string _) in peersByWallet.Values)
                    onWalletRemoved(wallet);

                peersByWallet.Clear();
                walletsByPeerId.Clear();
            }
        }

        public bool TryGetWallet(uint peerId, out Web3Address wallet)
        {
            lock (sync)
            {
                if (peersByWallet.TryGetValue(peerId, out (Web3Address wallet, string realm) entry))
                {
                    wallet = entry.wallet;
                    return true;
                }

                wallet = default(Web3Address);
                return false;
            }
        }

        /// <summary>
        ///     Fails when the peer is unknown or its announced realm differs from <paramref name="realm" />.
        /// </summary>
        public bool TryGetWalletInRealm(uint peerId, string realm, out Web3Address wallet)
        {
            lock (sync)
            {
                if (peersByWallet.TryGetValue(peerId, out (Web3Address wallet, string realm) entry) && entry.realm == realm)
                {
                    wallet = entry.wallet;
                    return true;
                }

                wallet = default(Web3Address);
                return false;
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
                foreach ((Web3Address wallet, string peerRealm) in peersByWallet.Values)
                    if (peerRealm != realm)
                        result.Add(wallet);
            }
        }

        /// <summary>
        ///     Atomically removes every peer whose announced realm differs from <paramref name="realm" />,
        ///     invoking the callback with each removed peer id.
        /// </summary>
        public void RemoveWhereNotInRealm(string realm, Action<uint> onPeerRemoved)
        {
            lock (sync)
            {
                removalBuffer.Clear();

                foreach (KeyValuePair<uint, (Web3Address wallet, string realm)> pair in peersByWallet)
                    if (pair.Value.realm != realm)
                        removalBuffer.Add(pair.Key);

                foreach (uint peerId in removalBuffer)
                {
                    if (peersByWallet.Remove(peerId, out (Web3Address wallet, string realm) entry))
                        walletsByPeerId.Remove(entry.wallet);

                    onPeerRemoved(peerId);
                }
            }
        }
    }
}
