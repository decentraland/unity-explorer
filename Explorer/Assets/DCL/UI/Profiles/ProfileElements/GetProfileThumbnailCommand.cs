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
                property.SetLoaded(cachedSprite, true);
                return;
            }

            // Wait until the property is bound
            while (property.Value.ThumbnailState == ProfileThumbnailViewModel.State.NOT_BOUND)
                await UniTask.Yield();

            if (ct.IsCancellationRequested)
                return;

            try
            {
                Sprite? downloadedSprite = await profileRepository.GetProfileThumbnailAsync(faceSnapshotUrl, ct);

                if (downloadedSprite != null)
                    property.SetLoaded(downloadedSprite, false);
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
                property.UpdateValue(fallback == null ? ProfileThumbnailViewModel.Error(property.Value.ProfileColor) : ProfileThumbnailViewModel.FromFallback(fallback, property.Value.ProfileColor));
            }
        }
    }
}
