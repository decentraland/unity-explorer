using DCL.Multiplayer.Profiles.Bunches;

namespace DCL.Multiplayer.Profiles.Announcements
{
    public interface IRemoteAnnouncements
    {
        Bunch<RemoteAnnouncement> Bunch();
    }
}
