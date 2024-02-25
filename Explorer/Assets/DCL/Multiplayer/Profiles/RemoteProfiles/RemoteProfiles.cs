using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Multiplayer.Profiles.RemoteAnnouncements;
using DCL.Profiles;
using DCL.Utilities.Extensions;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Multiplayer.Profiles.RemoteProfiles
{
    public class RemoteProfiles : IRemoteProfiles
    {
        private readonly IProfileRepository profileRepository;
        private readonly List<RemoteProfile> remoteProfiles = new ();

        public RemoteProfiles(IProfileRepository profileRepository)
        {
            this.profileRepository = profileRepository;
        }

        public void Download(ICollection<RemoteAnnouncement> list)
        {
            //TODO consider which option for performance would be better, just everything, to download or by chuncks, question about concurrency for web requests
            foreach (RemoteAnnouncement remoteAnnouncement in list)
                DownloadAsync(remoteAnnouncement).Forget();
        }

        public bool NewBunchAvailable() =>
            remoteProfiles.Count > 0;

        public Bunch<RemoteProfile> Bunch() =>
            new (remoteProfiles);

        private async UniTaskVoid DownloadAsync(RemoteAnnouncement remoteAnnouncement)
        {
            var profile = await profileRepository.GetAsync(remoteAnnouncement.WalletId, remoteAnnouncement.Version, CancellationToken.None);
            profile.EnsureNotNull($"Profile not found: {remoteAnnouncement.WalletId} {remoteAnnouncement.Version}");
            remoteProfiles.Add(new RemoteProfile(profile!, remoteAnnouncement.WalletId));
        }
    }
}
