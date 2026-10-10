using DCL.Multiplayer.Profiles.Announcements;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.Multiplayer.Profiles.RemoteProfiles
{
    public static class RemoteProfilesExtensions
    {
        public static void Download(this RemoteProfiles remoteProfiles, IRemoteAnnouncements remoteAnnouncements)
        {
            using PooledObject<List<RemoteAnnouncement>> _ = ListPool<RemoteAnnouncement>.Get(out List<RemoteAnnouncement>? announcements);

            remoteAnnouncements.Fill(announcements);

            if (announcements.Count > 0)
                remoteProfiles.Download(announcements);
        }
    }
}
