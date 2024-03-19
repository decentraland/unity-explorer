using Cysharp.Threading.Tasks;
using System;

namespace DCL.Multiplayer.Profiles.BroadcastProfiles
{
    public interface IProfileBroadcast : IDisposable
    {
        //TODO send profiles to only new connected remote participants

        UniTaskVoid NotifyRemotesAsync();
    }
}
