using DCL.Web3;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Pulse
{
    public class PeerIdCache
    {
        private readonly object sync = new ();
        private readonly Dictionary<uint, Web3Address> peersByWallet = new ();
        private readonly Dictionary<Web3Address, uint> walletsByPeerId = new ();

        public void Set(Web3Address wallet, uint peerId)
        {
            lock (sync)
            {
                peersByWallet[peerId] = wallet;
                walletsByPeerId[wallet] = peerId;
            }
        }

        public void Remove(uint peerId)
        {
            lock (sync)
            {
                if (peersByWallet.Remove(peerId, out Web3Address wallet))
                    walletsByPeerId.Remove(wallet);
            }
        }

        /// <summary>
        ///     Atomically iterates all wallets, invokes the callback for each, then clears both caches.
        /// </summary>
        public void RemoveAll(Action<string> onWalletRemoved)
        {
            lock (sync)
            {
                foreach (string wallet in peersByWallet.Values)
                    onWalletRemoved(wallet);

                peersByWallet.Clear();
                walletsByPeerId.Clear();
            }
        }

        public bool TryGetWallet(uint peerId, out Web3Address wallet)
        {
            lock (sync)
                return peersByWallet.TryGetValue(peerId, out wallet);
        }

        public bool TryGetPeerId(Web3Address wallet, out uint peerId)
        {
            lock (sync)
                return walletsByPeerId.TryGetValue(wallet, out peerId);
        }
    }
}
