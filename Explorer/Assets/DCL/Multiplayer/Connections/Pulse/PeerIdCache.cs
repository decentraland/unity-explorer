using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Pulse
{
    public class PeerIdCache
    {
        private readonly Dictionary<uint, string> peersByWallet = new ();
        private readonly Dictionary<string, uint> walletsByPeerId = new ();

        public void Set(string wallet, uint peerId)
        {
            peersByWallet[peerId] = wallet;
            walletsByPeerId[wallet] = peerId;
        }

        public void Remove(uint peerId)
        {
            if (peersByWallet.TryGetValue(peerId, out string? wallet))
                walletsByPeerId.Remove(wallet);
        }

        public bool TryGetWallet(uint peerId, out string wallet) =>
            peersByWallet.TryGetValue(peerId, out wallet);

        public bool TryGetPeerId(string wallet, out uint peerId) =>
            walletsByPeerId.TryGetValue(wallet, out peerId);
    }
}
