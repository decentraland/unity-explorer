using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Multiplayer.Profiles.RemoteAnnouncements;
using DCL.Profiles;
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

        public void Download(IReadOnlyCollection<RemoteAnnouncement> list)
        {
            //TODO consider which option for performance would be better, just everything, to download or by chuncks, question about concurrency for web requests
            foreach (RemoteAnnouncement remoteAnnouncement in list)
                TryDownloadAsync(remoteAnnouncement).Forget();
        }

        public bool NewBunchAvailable() =>
            remoteProfiles.Count > 0;

        public Bunch<RemoteProfile> Bunch() =>
            new (remoteProfiles);

        private async UniTaskVoid TryDownloadAsync(RemoteAnnouncement remoteAnnouncement)
        {
            Profile? profile = await profileRepository.GetAsync(remoteAnnouncement.WalletId, remoteAnnouncement.Version, CancellationToken.None);

            if (profile is null)
            {
                //TODO for some reason log error is not working
                ReportHub.LogError(ReportCategory.PROFILE, $"Profile not found {remoteAnnouncement}");
                return;
            }

            remoteProfiles.Add(new RemoteProfile(profile, remoteAnnouncement.WalletId));
        }
    }
}
