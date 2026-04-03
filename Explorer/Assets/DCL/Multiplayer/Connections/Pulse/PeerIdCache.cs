using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Pulse
{
    public class PeerIdCache
    {
        private readonly object sync = new ();
        private readonly Dictionary<uint, string> peersByWallet = new ();
        private readonly Dictionary<string, uint> walletsByPeerId = new ();

        public void Set(string wallet, uint peerId)
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
                if (peersByWallet.Remove(peerId, out string? wallet))
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

        public bool TryGetWallet(uint peerId, out string wallet)
        {
            lock (sync)
                return peersByWallet.TryGetValue(peerId, out wallet);
        }

        public bool TryGetPeerId(string wallet, out uint peerId)
        {
            lock (sync)
                return walletsByPeerId.TryGetValue(wallet, out peerId);
        }
    }
}
