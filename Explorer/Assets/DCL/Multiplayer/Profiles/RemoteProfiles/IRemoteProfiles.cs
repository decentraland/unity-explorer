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

    public static class RemoteProfilesExtensions
    {
        public static void Download(this IRemoteProfiles remoteProfiles, IRemoteAnnouncements remoteAnnouncements)
        {
            using Bunch<RemoteAnnouncement> bunch = remoteAnnouncements.Bunch();

            if (bunch.Available() == false)
                return;

            IReadOnlyCollection<RemoteAnnouncement> list = bunch.Collection();
            remoteProfiles.Download(list);
        }
    }
}
