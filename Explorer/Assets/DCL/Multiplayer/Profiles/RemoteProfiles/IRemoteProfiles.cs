using DCL.Multiplayer.Profiles.Bunches;
using DCL.Multiplayer.Profiles.RemoteAnnouncements;
using System.Collections.Generic;

namespace DCL.Multiplayer.Profiles.RemoteProfiles
{
    public interface IRemoteProfiles
    {
        void Download(IReadOnlyCollection<RemoteAnnouncement> list);

        bool NewBunchAvailable();

        Bunch<RemoteProfile> Bunch();
    }
}
