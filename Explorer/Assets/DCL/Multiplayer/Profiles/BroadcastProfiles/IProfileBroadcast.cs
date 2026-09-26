using System;

namespace DCL.Multiplayer.Profiles.BroadcastProfiles
{
    public interface IProfileBroadcast : IDisposable
    {
        void NotifyRemotes();
    }
}
