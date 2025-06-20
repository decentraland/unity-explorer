
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Profiles;
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
        private readonly IThumbnailCache thumbnailCache;
        private readonly IProfileRepository profileRepository;
        private readonly IRemoteMetadata remoteMetadata;

        public ProfileRepositoryWrapper(IProfileRepository profileRepository, IThumbnailCache thumbnailCache, IRemoteMetadata remoteMetadata)
        {
            this.thumbnailCache = thumbnailCache;
            this.profileRepository = profileRepository;
            this.remoteMetadata = remoteMetadata;
        }

        public async UniTask<Sprite?> GetProfileThumbnailAsync(string userId, string thumbnailUrl, CancellationToken ct) =>
            await thumbnailCache.GetThumbnailAsync(userId, thumbnailUrl, ct);

        public Sprite? GetProfileThumbnail(string userId) =>
            thumbnailCache.GetThumbnail(userId);

        public async UniTask<Profile?> GetProfileAsync(string userId, CancellationToken ct) =>
            await profileRepository.GetAsync(userId, 0, remoteMetadata.GetLambdaDomainOrNull(userId), ct);

    }
}
