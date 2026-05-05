using CodeLess.Attributes;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.UI.ProfileElements
{
    [Singleton]
    public partial class GetProfileThumbnailCommand
    {
        private readonly ProfileRepositoryWrapper profileRepository;

        public GetProfileThumbnailCommand(ProfileRepositoryWrapper profileRepository)
        {
            this.profileRepository = profileRepository;
        }

        public UniTask ExecuteAsync(IReactiveProperty<ProfileThumbnailViewModel> property, Sprite? fallback, Profile.CompactInfo profile, CancellationToken ct) =>
            ExecuteAsync(property, fallback, profile.UserId, profile.FaceSnapshotUrl, ct);

        public async UniTask ExecuteAsync(IReactiveProperty<ProfileThumbnailViewModel> property, Sprite? fallback, string userId, string faceSnapshotUrl, CancellationToken ct)
        {
            // We don't need to wait (and skip frames) until the property is bound if the data is already cached.

            Sprite? cachedSprite = profileRepository.GetProfileThumbnail(faceSnapshotUrl);

            if (cachedSprite != null)
            {
                profileRepository.StoreLatestThumbnailUrlForUser(userId, faceSnapshotUrl);
                property.SetLoaded(cachedSprite, true);
                return;
            }

            // Wait until the property is bound
            while (property.Value.ThumbnailState == ProfileThumbnailViewModel.State.NOT_BOUND)
                await UniTask.Yield();

            if (ct.IsCancellationRequested)
                return;

            // Seed with this user's last known sprite (null if none) so we keep their previous picture during download
            // and don't leak a sprite from a previous user when the property is reused (e.g. identity change).
            Sprite? previousUserSprite = profileRepository.GetLatestThumbnailForUser(userId);

            property.UpdateValue(new ProfileThumbnailViewModel(ProfileThumbnailViewModel.State.LOADING, previousUserSprite, property.Value.ProfileColor, property.Value.FitAndCenterImage));

            try
            {
                Sprite? downloadedSprite = await profileRepository.GetProfileThumbnailAsync(faceSnapshotUrl, ct);

                if (downloadedSprite != null)
                {
                    profileRepository.StoreLatestThumbnailUrlForUser(userId, faceSnapshotUrl);
                    property.SetLoaded(downloadedSprite, false);
                }
                else
                    UpdateFromError();
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.UI, $"Thumbnail download failed for {userId}: {e.Message}");
                UpdateFromError();
            }

            void UpdateFromError()
            {
                // If we already swapped in a previous picture, keep showing it instead of falling back to the placeholder.
                if (property.Value.Sprite != null && property.Value.ThumbnailState == ProfileThumbnailViewModel.State.LOADING)
                    return;

                property.UpdateValue(fallback == null ? ProfileThumbnailViewModel.Error(property.Value.ProfileColor) : ProfileThumbnailViewModel.FromFallback(fallback, property.Value.ProfileColor));
            }
        }
    }
}
