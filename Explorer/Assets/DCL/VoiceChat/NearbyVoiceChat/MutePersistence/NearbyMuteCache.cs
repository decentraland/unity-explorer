using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.VoiceChat.Nearby.MutePersistence
{
    /// <summary>
    /// All wallet ids stored in this cache are lowercase. Normalization happens on write
    /// (<see cref="SetMuted"/>, <see cref="Merge"/>); reads trust the input. The upstream
    /// contract (catalyst profiles, social-service mutes endpoint) returns lowercase 0x-hex,
    /// so the defensive <c>ToLowerInvariant</c> on write protects against EIP-55 checksummed
    /// addresses without paying the upper-fold cost on every <see cref="IsMuted"/> call.
    /// <para>
    ///     <b>Thread-safety.</b> All <see cref="HashSet{T}"/> access is serialized through <see cref="gate"/>.
    ///     Hot-path reads (<see cref="IsMuted"/>, <see cref="Version"/>) compete only against the one-shot
    ///     <see cref="NearbyMuteService.LoadAsync"/>-driven <see cref="Merge"/> at startup and the rare
    ///     UI-driven <see cref="SetMuted"/>, so contention is effectively zero. <see cref="MuteStateChanged"/>
    ///     is invoked outside the lock to keep subscriber callbacks (which may re-enter cache APIs or take
    ///     other locks) deadlock-free.
    /// </para>
    /// </summary>
    public class NearbyMuteCache : INearbyMuteCache
    {
        private readonly object gate = new ();
        private readonly HashSet<string> mutedWalletIds = new (StringComparer.Ordinal);
        private readonly HashSet<string> sessionUnmuted = new (StringComparer.Ordinal); // Addresses the user explicitly unmuted in this session.

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
            string normalized = walletAddress.ToLowerInvariant();

            lock (gate)
            {
                if (muted)
                {
                    if (mutedWalletIds.Add(normalized))
                        version++;

                    sessionUnmuted.Remove(normalized);
                }
                else
                {
                    if (mutedWalletIds.Remove(normalized))
                        version++;

                    sessionUnmuted.Add(normalized); // Record intent even if not currently in the cache — Merge must honour it.
                }
            }
        }

        public void Merge(IEnumerable<string> mutedAddresses)
        {
            // Client is source of truth: server snapshot only adds missing entries.
            // Skip addresses the user explicitly unmuted this session, otherwise a stale snapshot would re-mute them.
            // Two-phase: collect added entries under the lock, then fire events outside it (deadlock-free, matches SetMuted's discipline).
            // The added list is allocated once per Merge call (init-only path).
            List<string>? added = null;

            lock (gate)
            {
                foreach (string address in mutedAddresses)
                {
                    string normalized = address.ToLowerInvariant();

                    if (sessionUnmuted.Contains(normalized)) continue;

                    if (mutedWalletIds.Add(normalized))
                    {
                        version++;
                        (added ??= new List<string>()).Add(normalized);
                    }
                }
            }
        }
    }
}
