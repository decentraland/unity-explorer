using DCL.VoiceChat.Nearby.MutePersistence;
using System;
using System.Collections.Generic;

namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    /// Minimal HashSet-backed <see cref="INearbyMuteCache"/> for performance tests. Replaces
    /// <c>Substitute.For&lt;INearbyMuteCache&gt;()</c> in any benchmark whose measured loop calls
    /// <see cref="NearbyMuteService.IsMuted"/>: NSubstitute proxies every call through
    /// argument-matching + call-recording (~20 µs/call + ~10 B GC) which inflates "system Update"
    /// benchmarks ~×7 over true production cost (single dispatch + <see cref="HashSet{T}.Contains"/>).
    /// <para>
    /// Used by:
    /// <list type="bullet">
    ///   <item><c>NearbyAudioPositionHotPathPerformanceTest.MuteService_Lookups</c> — isolated slice</item>
    ///   <item><c>NearbyAudioPositionSystemPerformanceTest.UpdateWithNParticipants</c> — full system Update</item>
    ///   <item><c>NearbyAudioFullCyclePerformanceTest</c> — full Binding→Position→Cleanup chain</item>
    /// </list>
    /// </para>
    /// </summary>
    internal sealed class FakeMuteCache : INearbyMuteCache
    {
        private readonly HashSet<string> mutedSet = new ();

        // Mirrors production NearbyMuteCache: starts at 1 so a freshly-bound component
        // (LastSeenMuteVersion=0) deterministically mismatches on its first tick.
        public uint Version { get; private set; } = 1;

        public event Action<string, bool>? MuteStateChanged;

        public bool IsMuted(string walletAddress) =>
            mutedSet.Contains(walletAddress);

        public void SetMuted(string walletAddress, bool muted)
        {
            bool changed = muted ? mutedSet.Add(walletAddress) : mutedSet.Remove(walletAddress);
            if (changed)
            {
                Version++;
                MuteStateChanged?.Invoke(walletAddress, muted);
            }
        }

        public void Merge(IEnumerable<string> mutedAddresses)
        {
            foreach (string addr in mutedAddresses)
                if (mutedSet.Add(addr))
                {
                    Version++;
                    MuteStateChanged?.Invoke(addr, true);
                }
        }
    }
}
