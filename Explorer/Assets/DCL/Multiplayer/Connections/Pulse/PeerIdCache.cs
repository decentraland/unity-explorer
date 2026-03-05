using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Pulse
{
    public class PeerIdCache
    {
        private readonly Dictionary<uint, string> peersByWallet = new ();

        public void Set(string wallet, uint peerId) =>
            peersByWallet[peerId] = wallet;

        public bool TryGetWallet(uint peerId, out string wallet) =>
            peersByWallet.TryGetValue(peerId, out wallet);
    }
}
