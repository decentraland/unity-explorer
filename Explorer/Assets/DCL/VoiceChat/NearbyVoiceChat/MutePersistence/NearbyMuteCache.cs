using System;
using System.Collections.Generic;

namespace DCL.VoiceChat.Nearby.MutePersistence
{
    public class NearbyMuteCache : INearbyMuteCache
    {
        private readonly HashSet<string> mutedWalletIds = new (StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> sessionUnmuted = new (StringComparer.OrdinalIgnoreCase); // Addresses the user explicitly unmuted in this session.

        public event Action<string, bool>? MuteStateChanged;

        public bool IsMuted(string walletAddress) =>
            mutedWalletIds.Contains(walletAddress);

        public void SetMuted(string walletAddress, bool muted)
        {
            if (muted)
            {
                if (mutedWalletIds.Add(walletAddress))
                    MuteStateChanged?.Invoke(walletAddress, true);

                sessionUnmuted.Remove(walletAddress);
            }
            else
            {
                if (mutedWalletIds.Remove(walletAddress))
                    MuteStateChanged?.Invoke(walletAddress, false);

                // Record intent even if not currently in the cache — Merge must honour it.
                sessionUnmuted.Add(walletAddress);
            }
        }

        public void Merge(IEnumerable<string> mutedAddresses)
        {
            // Client is source of truth: server snapshot only adds missing entries.
            // Skip addresses the user explicitly unmuted this session, otherwise a stale snapshot would re-mute them.
            foreach (string address in mutedAddresses)
            {
                if (sessionUnmuted.Contains(address)) continue;

                if (mutedWalletIds.Add(address))
                    MuteStateChanged?.Invoke(address, true);
            }
        }
    }
}
