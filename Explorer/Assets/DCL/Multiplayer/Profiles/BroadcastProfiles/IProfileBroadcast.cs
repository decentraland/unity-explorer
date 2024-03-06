using Cysharp.Threading.Tasks;

namespace DCL.Multiplayer.Profiles.BroadcastProfiles
{
    public interface IProfileBroadcast
    {
        //TODO send profiles to only new connected remote participants

        UniTaskVoid NotifyRemotesAsync();
    }
}
