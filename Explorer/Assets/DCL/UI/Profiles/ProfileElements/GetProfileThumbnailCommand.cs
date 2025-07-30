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

        public UniTask ExecuteAsync(IReactiveProperty<ProfileThumbnailViewModel> property, Sprite? fallback, string userId, string faceSnapshotUrl,
            CancellationToken ct) =>
            ExecuteAsync(property, static (p, vm) => p.UpdateValue(vm), static p => p.Value, fallback, userId, faceSnapshotUrl, ct);

        public UniTask ExecuteAsync(IReactiveProperty<ProfileThumbnailViewModel.WithColor> property, Sprite? fallback, string userId, string faceSnapshotUrl,
            CancellationToken ct) =>
            ExecuteAsync(property, static (p, vm) => p.UpdateValue(p.Value.SetProfile(vm)), static p => p.Value.Thumbnail, fallback, userId, faceSnapshotUrl, ct);

        private async UniTask ExecuteAsync<T>(IReactiveProperty<T> property, Action<IReactiveProperty<T>, ProfileThumbnailViewModel> setProfile, Func<IReactiveProperty<T>, ProfileThumbnailViewModel> getProfile,
            Sprite? fallback, string userId, string faceSnapshotUrl, CancellationToken ct)
        {
            // Wait until the property is bound
            while (getProfile(property).ThumbnailState == ProfileThumbnailViewModel.State.NOT_BOUND)
                await UniTask.Yield();

            if (ct.IsCancellationRequested)
                return;

            Sprite? cachedSprite = profileRepository.GetProfileThumbnail(userId);

            if (cachedSprite != null)
            {
                setProfile(property, ProfileThumbnailViewModel.FromLoaded(cachedSprite, true));
                return;
            }

            try
            {
                Sprite? downloadedSprite = await profileRepository.GetProfileThumbnailAsync(faceSnapshotUrl, ct);

                if (downloadedSprite != null)
                    setProfile(property, ProfileThumbnailViewModel.FromLoaded(downloadedSprite, false));
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
                setProfile(property, fallback == null ? ProfileThumbnailViewModel.Error() : ProfileThumbnailViewModel.FromFallback(fallback));
            }
        }
    }
}
