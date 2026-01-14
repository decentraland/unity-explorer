
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Profiles;
using DCL.WebRequests;
using System.Threading;
using UnityEngine;

namespace DCL.UI.Profiles.Helpers
{
    /// <summary>
    /// It provides some simplified ways to get profile-related data that is needed in many places of the UI, without exposing system services.
    /// </summary>
    /// <remarks>
    /// Note: This was extracted from ViewDependencies, as that was not the right place for this functionality to be.
    /// </remarks>
    public class ProfileRepositoryWrapper
    {
        // We need to set a delay due to the time that takes to regenerate the thumbnail at the backend
        // It is incremental, as the time to process it varies depending on the traffic
        private static readonly RetryPolicy RETRY_POLICY = RetryPolicy.Enforce(10, 10_000, 2, IWebRequestController.IGNORE_NOT_FOUND);


        private readonly ISpriteCache thumbnailCache;
        private readonly IProfileRepository profileRepository;
        private readonly IRemoteMetadata remoteMetadata;

        public ProfileRepositoryWrapper(IProfileRepository profileRepository, ISpriteCache thumbnailCache, IRemoteMetadata remoteMetadata)
        {
            this.thumbnailCache = thumbnailCache;
            this.profileRepository = profileRepository;
            this.remoteMetadata = remoteMetadata;
        }

        public async UniTask<Sprite?> GetProfileThumbnailAsync(string thumbnailUrl, CancellationToken ct) =>
            await thumbnailCache.GetSpriteAsync(thumbnailUrl, RETRY_POLICY, ct);

        public Sprite? GetProfileThumbnail(string thumbnailUrl) =>
            thumbnailCache.GetCachedSprite(thumbnailUrl);

        public async UniTask<Profile?> GetProfileAsync(string userId, CancellationToken ct) =>
            await profileRepository.GetAsync(userId, 0, remoteMetadata.GetLambdaDomainOrNull(userId), ct);

    }
}
