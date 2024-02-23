using DCL.Multiplayer.Profiles.Bunches;

namespace DCL.Multiplayer.Profiles.RemoteAnnouncements
{
    public interface IRemoteAnnouncements
    {
        bool NewBunchAvailable();

        OwnedBunch<RemoteAnnouncement> Bunch();
    }
}
