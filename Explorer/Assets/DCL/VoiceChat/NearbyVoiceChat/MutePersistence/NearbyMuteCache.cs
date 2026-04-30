using System;
using System.Collections.Generic;

namespace DCL.VoiceChat.Nearby.MutePersistence
{
    /// <summary>
    /// All wallet ids stored in this cache are lowercase. Normalization happens on write
    /// (<see cref="SetMuted"/>, <see cref="Merge"/>); reads trust the input. The upstream
    /// contract (catalyst profiles, social-service mutes endpoint) returns lowercase 0x-hex,
    /// so the defensive <c>ToLowerInvariant</c> on write protects against EIP-55 checksummed
    /// addresses without paying the upper-fold cost on every <see cref="IsMuted"/> call.
    /// </summary>
    public class NearbyMuteCache : INearbyMuteCache
    {
        private readonly HashSet<string> mutedWalletIds = new (StringComparer.Ordinal);
        private readonly HashSet<string> sessionUnmuted = new (StringComparer.Ordinal); // Addresses the user explicitly unmuted in this session.

        // Starts at 1 so a freshly-constructed component (LastSeenMuteVersion=0) deterministically
        // mismatches on its first tick — guarantees the pessimistic initial recompute.
        public uint Version { get; private set; } = 1;

        public event Action<string, bool>? MuteStateChanged;

        public bool IsMuted(string walletAddress) =>
            mutedWalletIds.Contains(walletAddress);

        public void SetMuted(string walletAddress, bool muted)
        {
            string normalized = walletAddress.ToLowerInvariant();

            if (muted)
            {
                if (mutedWalletIds.Add(normalized))
                {
                    Version++;
                    MuteStateChanged?.Invoke(normalized, true);
                }

                sessionUnmuted.Remove(normalized);
            }
            else
            {
                if (mutedWalletIds.Remove(normalized))
                {
                    Version++;
                    MuteStateChanged?.Invoke(normalized, false);
                }

                // Record intent even if not currently in the cache — Merge must honour it.
                sessionUnmuted.Add(normalized);
            }
        }

        public void Merge(IEnumerable<string> mutedAddresses)
        {
            // Client is source of truth: server snapshot only adds missing entries.
            // Skip addresses the user explicitly unmuted this session, otherwise a stale snapshot would re-mute them.
            foreach (string address in mutedAddresses)
            {
                string normalized = address.ToLowerInvariant();

                if (sessionUnmuted.Contains(normalized)) continue;

                if (mutedWalletIds.Add(normalized))
                {
                    Version++;
                    MuteStateChanged?.Invoke(normalized, true);
                }
            }
        }
    }
}
