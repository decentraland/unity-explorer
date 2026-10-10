using System;
using System.Collections.Generic;

namespace DCL.VoiceChat.Nearby.MutePersistence
{
    public interface INearbyMuteCache
    {
        /// <summary>
        /// Monotonically increases on every mutation that actually changes the muted set.
        /// Idempotent calls (e.g. muting an already-muted address) do not bump it.
        /// Read-side consumers can compare a cached value against this to skip work entirely while the cache is unchanged.
        /// </summary>
        uint Version { get; }

        /// <summary>
        /// Fires on every state-flip — push channel alongside the pull-based <see cref="Version"/>.
        /// Idempotent writes (mute already-muted) do not raise it.
        /// Parameters: walletAddress, isMuted.
        /// </summary>
        event Action<string, bool>? MuteStateChanged;

        bool IsMuted(string walletAddress);

        void SetMuted(string walletAddress, bool muted);

        void Merge(IEnumerable<string> mutedAddresses);
    }
}
