using CodeLess.Attributes;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
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

        public async UniTask ExecuteAsync(IReactiveProperty<ProfileThumbnailViewModel> property, Sprite? fallback, string userId, string faceSnapshotUrl, CancellationToken ct)
        {
            Sprite? cachedSprite = profileRepository.GetProfileThumbnail(userId);

            if (cachedSprite != null)
            {
                property.UpdateValue(ProfileThumbnailViewModel.FromLoaded(cachedSprite, true));
                return;
            }

            try
            {
                Sprite? downloadedSprite = await profileRepository.GetProfileThumbnailAsync(faceSnapshotUrl, ct);

                if (downloadedSprite != null)
                    property.UpdateValue(ProfileThumbnailViewModel.FromLoaded(downloadedSprite, false));
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
                property.UpdateValue(fallback == null ? ProfileThumbnailViewModel.Error() : ProfileThumbnailViewModel.FromFallback(fallback));
            }
        }
    }
}
