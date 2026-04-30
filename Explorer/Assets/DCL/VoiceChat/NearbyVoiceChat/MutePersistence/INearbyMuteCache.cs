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

        bool IsMuted(string walletAddress);

        void SetMuted(string walletAddress, bool muted);

        void Merge(IEnumerable<string> mutedAddresses);
    }
}
