using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Multiplayer.Profiles.RemoteAnnouncements;
using DCL.Optimization.Pools;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.Multiplayer.Profiles.RemoteProfiles
{
    public class RemoteProfiles : IRemoteProfiles
    {
        private readonly struct PendingRequest
        {
            public readonly int Version;
            public readonly CancellationTokenSource Cts;
            public readonly DateTime StartedAt;

            public PendingRequest(int version, CancellationTokenSource cts, DateTime startedAt)
            {
                Version = version;
                Cts = cts;
                StartedAt = startedAt;
            }
        }

        private readonly IProfileRepository profileRepository;
        private readonly List<RemoteProfile> remoteProfiles = new ();
        private readonly Dictionary<string, PendingRequest> pendingProfiles = new (PoolConstants.AVATARS_COUNT);
        private readonly IRemoteMetadata remoteMetadata;

        public RemoteProfiles(IProfileRepository profileRepository, IRemoteMetadata remoteMetadata)
        {
            this.profileRepository = profileRepository;
            this.remoteMetadata = remoteMetadata;
        }

        public void Download(IReadOnlyCollection<RemoteAnnouncement> list)
        {
            foreach (RemoteAnnouncement remoteAnnouncement in list)
                TryDownloadAsync(remoteAnnouncement).Forget();
        }

        public bool NewBunchAvailable() =>
            remoteProfiles.Count > 0;

        public Bunch<RemoteProfile> Bunch() =>
            new (remoteProfiles);

        private async UniTaskVoid TryDownloadAsync(RemoteAnnouncement remoteAnnouncement)
        {
            URLDomain? lambdasEndpoint = remoteMetadata.GetLambdaDomainOrNull(remoteAnnouncement.WalletId);

            DateTime startedAt = DateTime.Now;

            if (pendingProfiles.TryGetValue(remoteAnnouncement.WalletId, out PendingRequest pendingRequest))
            {
                if (pendingRequest.Version < remoteAnnouncement.Version)
                {
                    ReportHub.Log(ReportCategory.PROFILE,
                        $"Profile announcement {remoteAnnouncement.WalletId} V:{pendingRequest.Version} was super-seeded by V:{remoteAnnouncement.Version} "
                        + $"after {(startedAt - pendingRequest.StartedAt).TotalSeconds} s.");

                    // Cancel the request with older version, it's no longer valid
                    pendingRequest.Cts.Cancel();
                }
                else

                    // Nothing to do, the new one is already being requested
                    return;
            }

            var cts = new CancellationTokenSource();
            pendingProfiles[remoteAnnouncement.WalletId] = new PendingRequest(remoteAnnouncement.Version, cts, startedAt);

            try
            {
                Profile? profile = await profileRepository.GetAsync(remoteAnnouncement.WalletId, remoteAnnouncement.Version, lambdasEndpoint, cts.Token);

                if (profile is null)
                {
                    ReportHub.LogError(ReportCategory.PROFILE, $"Profile not found {remoteAnnouncement} after {(DateTime.Now - startedAt).TotalSeconds} s.");
                    return;
                }

                remoteProfiles.Add(new RemoteProfile(profile, remoteAnnouncement.WalletId));

                ReportHub.Log(ReportCategory.PROFILE,
                    $"{remoteAnnouncement} was downloaded for {(DateTime.Now - startedAt).TotalSeconds} s.");
            }
            catch (OperationCanceledException)
            {
                // ignore cancellation
            }
            catch (Exception)
            {
                ReportHub.Log(ReportCategory.PROFILE,
                    $"{remoteAnnouncement} threw an exception after {(DateTime.Now - startedAt).TotalSeconds} s.");

                throw;
            }
            finally
            {
                // Just an additional protection if the inner request was cancelled while being off the main thread
                await UniTask.SwitchToMainThread();

                // Clean-up pending profile request only if it is the same version (not overriden)
                if (pendingProfiles.TryGetValue(remoteAnnouncement.WalletId, out pendingRequest) && pendingRequest.Version == remoteAnnouncement.Version)
                    pendingProfiles.Remove(remoteAnnouncement.WalletId);
            }
        }
    }
}
