using DCL.Multiplayer.Profiles.Announcements;
using DCL.Multiplayer.Profiles.Bunches;
using System.Collections.Generic;

namespace DCL.Multiplayer.Profiles.RemoteProfiles
{
    public static class RemoteProfilesExtensions
    {
        public static void Download(this RemoteProfiles remoteProfiles, RemoteAnnouncements.RemoteAnnouncements remoteAnnouncements)
        {
            using Bunch<RemoteAnnouncement> bunch = remoteAnnouncements.Bunch();

            if (bunch.Available() == false)
                return;

            IReadOnlyCollection<RemoteAnnouncement> list = bunch.Collection();
            remoteProfiles.Download(list);
        }
    }
}
