using System;
using System.Collections.Generic;

namespace DCL.VoiceChat.MutePersistence
{
    public interface IProximityMuteCache
    {
        event Action<string, bool>? MuteStateChanged;

        bool IsMuted(string walletAddress);

        void SetMuted(string walletAddress, bool muted);

        void Reset(IEnumerable<string> mutedAddresses);
    }
}
