using System;
using System.Collections.Generic;

namespace DCL.VoiceChat.Nearby.MutePersistence
{
    public class NearbyMuteCache : INearbyMuteCache
    {
        private readonly HashSet<string> mutedWalletIds = new (StringComparer.OrdinalIgnoreCase);

        public event Action<string, bool>? MuteStateChanged;

        public bool IsMuted(string walletAddress) =>
            mutedWalletIds.Contains(walletAddress);

        public void SetMuted(string walletAddress, bool muted)
        {
            bool changed = muted
                ? mutedWalletIds.Add(walletAddress)
                : mutedWalletIds.Remove(walletAddress);

            if (changed)
                MuteStateChanged?.Invoke(walletAddress, muted);
        }

        public void Reset(IEnumerable<string> mutedAddresses)
        {
            mutedWalletIds.Clear();

            foreach (string address in mutedAddresses)
                mutedWalletIds.Add(address);
        }
    }
}
