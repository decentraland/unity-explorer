using Cysharp.Threading.Tasks;
using DCL.UI.Profiles.Helpers;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.UI.ProfileElements
{
    public class ProfilePictureView : MonoBehaviour, IDisposable
    {
        [SerializeField] private ImageView thumbnailImageView;
        [SerializeField] private Image thumbnailBackground;
        [SerializeField] private Sprite defaultEmptyThumbnail;

        private ProfileRepositoryWrapper profileRepositoryWrapper;
        private CancellationTokenSource? cts;
        private string? currentUserId;

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
        }

        public void Setup(Color userColor, string faceSnapshotUrl, string userId)
        {
            SetupOnlyColor(userColor);
            LoadThumbnailAsync(faceSnapshotUrl, userId).Forget();
        }

        public async UniTask SetupWithDependenciesAsync(ProfileRepositoryWrapper profileDataProvider, Color userColor, string faceSnapshotUrl, string userId, CancellationToken ct)
        {
            this.profileRepositoryWrapper = profileDataProvider;
            SetupOnlyColor(userColor);
            await LoadThumbnailAsync(faceSnapshotUrl, userId, ct);
        }

        public void SetupWithDependencies(ProfileRepositoryWrapper profileDataProvider, Color userColor, string faceSnapshotUrl, string userId)
        {
            this.profileRepositoryWrapper = profileDataProvider;
            Setup(userColor, faceSnapshotUrl, userId);
        }

        public void SetupOnlyColor(Color userColor)
        {
            thumbnailBackground.color = userColor;
        }

        public void SetLoadingState(bool isLoading)
        {
            thumbnailImageView.IsLoading = isLoading;
            thumbnailImageView.ImageEnabled = !isLoading;
        }

        public void SetDefaultThumbnail()
        {
            thumbnailImageView.SetImage(defaultEmptyThumbnail);
            currentUserId = null;
        }

        public void SetProfileDataProvider(ProfileRepositoryWrapper profileDataProvider)
        {
            this.profileRepositoryWrapper = profileDataProvider;
        }

        private async UniTask SetThumbnailImageWithAnimationAsync(Sprite sprite, CancellationToken ct)
        {
            thumbnailImageView.SetImage(sprite);
            thumbnailImageView.ImageEnabled = true;
            await thumbnailImageView.FadeInAsync(0.5f, ct);
        }

        private async UniTask LoadThumbnailAsync(string faceSnapshotUrl, string userId, CancellationToken ct = default)
        {
            if (userId.Equals(currentUserId)) return;

            cts = ct != default ? cts.SafeRestartLinked(ct) : cts.SafeRestart();
            currentUserId = userId;

            try
            {
                ct.ThrowIfCancellationRequested();

                Sprite? sprite = profileRepositoryWrapper.GetProfileThumbnail(userId);

                if (sprite != null)
                {
                    thumbnailImageView.SetImage(sprite);
                    SetLoadingState(false);
                    thumbnailImageView.Alpha = 1f;
                    return;
                }

                SetLoadingState(true);
                thumbnailImageView.Alpha = 0f;

                sprite = await profileRepositoryWrapper.GetProfileThumbnailAsync(userId, faceSnapshotUrl, cts.Token);

                if (sprite == null)
                    currentUserId = null;

                await SetThumbnailImageWithAnimationAsync(sprite ? sprite! : defaultEmptyThumbnail, cts.Token);
            }
            catch (OperationCanceledException)
            {
                currentUserId = null;
            }
            catch (Exception e)
            {
                currentUserId = null;
                await SetThumbnailImageWithAnimationAsync(defaultEmptyThumbnail, cts.Token);
            }
        }
    }
}
