using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Web3.Identities;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
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
    public class ProfileRepositoryWrapper : IDisposable
    {
        // We need to set a delay due to the time that takes to regenerate the thumbnail at the backend
        // It is incremental, as the time to process it varies depending on the traffic
        private static readonly RetryPolicy RETRY_POLICY = RetryPolicy.Enforce(10, 10_000, 2, IWebRequestController.IGNORE_NOT_FOUND);

        private readonly ISpriteCache thumbnailCache;
        private readonly IProfileRepository profileRepository;
        private readonly IProfileCache profileCache;
        private readonly IWeb3IdentityCache identityCache;

        // Lets us keep showing a user's previous picture (via SpriteCache) while a newly published one is still being generated.
        private readonly Dictionary<string, string> latestThumbnailUrlByUser = new ();

        public event Action<string>? UserThumbnailRefreshed;

        public ProfileRepositoryWrapper(IProfileRepository profileRepository, IProfileCache profileCache, ISpriteCache thumbnailCache, IWeb3IdentityCache identityCache)
        {
            this.thumbnailCache = thumbnailCache;
            this.profileRepository = profileRepository;
            this.profileCache = profileCache;
            this.identityCache = identityCache;

            identityCache.OnIdentityCleared += OnIdentityCleared;
        }

        public void Dispose() =>
            identityCache.OnIdentityCleared -= OnIdentityCleared;

        private void OnIdentityCleared() =>
            latestThumbnailUrlByUser.Clear();

        public async UniTask<Sprite?> GetProfileThumbnailAsync(string thumbnailUrl, CancellationToken ct) =>
            await thumbnailCache.GetSpriteAsync(thumbnailUrl, RETRY_POLICY, ct);

        public Sprite? GetProfileThumbnail(string thumbnailUrl) =>
            thumbnailCache.GetCachedSprite(thumbnailUrl);

        public Sprite? GetLatestThumbnailForUser(string userId)
        {
            if (!latestThumbnailUrlByUser.TryGetValue(userId, out string? url))
                return null;

            return thumbnailCache.GetCachedSprite(url);
        }

        public void StoreLatestThumbnailUrlForUser(string userId, string thumbnailUrl)
        {
            // Reject stale writes: a concurrent fetch may finish with an older snapshot's URL after the profile cache
            // has already moved on. Trusting the cache here prevents the picture from rolling backwards.
            if (profileCache.TryGetCompact(userId, out Profile.CompactInfo cachedProfile)
                && !string.IsNullOrEmpty(cachedProfile.FaceSnapshotUrl.Value)
                && cachedProfile.FaceSnapshotUrl.Value != thumbnailUrl)
                return;

            bool hasExisting = latestThumbnailUrlByUser.TryGetValue(userId, out string? existingUrl);
            latestThumbnailUrlByUser[userId] = thumbnailUrl;

            if (hasExisting && existingUrl != thumbnailUrl)
                UserThumbnailRefreshed?.Invoke(userId);
        }

        public UniTask<Profile.CompactInfo?> GetProfileAsync(string userId, CancellationToken ct) =>
            profileRepository.GetCompactAsync(userId, ct);

        public UniTask<List<Profile.CompactInfo>> GetProfilesAsync(IReadOnlyList<string> userIds, CancellationToken ct) =>
            profileRepository.GetCompactAsync(userIds, ct);
    }
}
