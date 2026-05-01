using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.VoiceChat.Nearby.MutePersistence
{
    public class NearbyMuteCache : INearbyMuteCache
    {
        private readonly object gate = new ();
        private readonly HashSet<string> mutedWalletIds = new (StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> sessionUnmuted = new (StringComparer.OrdinalIgnoreCase); // Addresses the user explicitly unmuted in this session.

        // Starts at 1 so a freshly-constructed component (LastSeenMuteVersion=0) deterministically mismatches on its first tick — guarantees the pessimistic initial recompute.
        // All writes happen under `gate`; the lock-free Version getter uses Volatile.Read to observe the latest committed value.
        private uint version = 1;

        public uint Version => Volatile.Read(ref version);

        public bool IsMuted(string walletAddress)
        {
            lock (gate)
            {
                return mutedWalletIds.Contains(walletAddress);
            }
        }

        public void SetMuted(string walletAddress, bool muted)
        {
            lock (gate)
            {
                if (muted)
                {
                    if (mutedWalletIds.Add(walletAddress))
                        version++;

                    sessionUnmuted.Remove(walletAddress);
                }
                else
                {
                    if (mutedWalletIds.Remove(walletAddress))
                        version++;

                    sessionUnmuted.Add(walletAddress); // Record intent even if not currently in the cache — Merge must honour it.
                }
            }
        }

        public void Merge(IEnumerable<string> mutedAddresses)
        {
            // Client is source of truth: server snapshot only adds missing entries.
            // Skip addresses the user explicitly unmuted this session, otherwise a stale snapshot would re-mute them.
            lock (gate)
            {
                foreach (string address in mutedAddresses)
                {
                    if (sessionUnmuted.Contains(address)) continue;

                    if (mutedWalletIds.Add(address))
                        version++;
                }
            }
        }
    }
}
