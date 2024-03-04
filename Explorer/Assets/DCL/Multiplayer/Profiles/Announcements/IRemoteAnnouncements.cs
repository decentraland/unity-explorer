using DCL.Multiplayer.Profiles.Bunches;

namespace DCL.Multiplayer.Profiles.RemoteAnnouncements
{
    public interface IRemoteAnnouncements
    {
        OwnedBunch<RemoteAnnouncement> Bunch();
    }
}
