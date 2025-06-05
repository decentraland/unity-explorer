using Cysharp.Threading.Tasks;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.UI.ProfileElements
{
    public class ProfilePictureView : MonoBehaviour, IViewWithGlobalDependencies, IDisposable
    {
        [SerializeField] private ImageView thumbnailImageView;
        [SerializeField] private Image thumbnailBackground;
        [SerializeField] private Sprite defaultEmptyThumbnail;

        private ViewDependencies? viewDependencies;
        private CancellationTokenSource? cts;
        private string? currentThumbnailUrl;

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
        }

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
        }

        public void Setup(Color userColor, string faceSnapshotUrl, string userId)
        {
            SetupOnlyColor(userColor);
            LoadThumbnailAsync(faceSnapshotUrl).Forget();
        }

        public async UniTask SetupWithDependenciesAsync(ViewDependencies dependencies, Color userColor, string faceSnapshotUrl, string userId, CancellationToken ct)
        {
            InjectDependencies(dependencies);
            SetupOnlyColor(userColor);
            await LoadThumbnailAsync(faceSnapshotUrl, ct);
        }

        public void SetupWithDependencies(ViewDependencies dependencies, Color userColor, string faceSnapshotUrl, string userId)
        {
            InjectDependencies(dependencies);
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
            currentThumbnailUrl = null;
        }

        private async UniTask SetThumbnailImageWithAnimationAsync(Sprite sprite, CancellationToken ct)
        {
            thumbnailImageView.SetImage(sprite);
            thumbnailImageView.ImageEnabled = true;
            await thumbnailImageView.FadeInAsync(0.5f, ct);
        }

        private async UniTask LoadThumbnailAsync(string faceSnapshotUrl, CancellationToken ct = default)
        {
            if (faceSnapshotUrl.Equals(currentThumbnailUrl)) return;

            cts = ct != default ? cts.SafeRestartLinked(ct) : cts.SafeRestart();
            currentThumbnailUrl = faceSnapshotUrl;

            try
            {
                ct.ThrowIfCancellationRequested();

                Sprite? sprite = viewDependencies!.GetCachedProfileThumbnail(faceSnapshotUrl);

                if (sprite != null)
                {
                    thumbnailImageView.SetImage(sprite);
                    SetLoadingState(false);
                    thumbnailImageView.Alpha = 1f;
                    return;
                }

                SetLoadingState(true);
                thumbnailImageView.Alpha = 0f;

                sprite = await viewDependencies!.GetProfileThumbnailAsync(faceSnapshotUrl, cts.Token);

                if (sprite == null)
                    currentThumbnailUrl = null;

                await SetThumbnailImageWithAnimationAsync(sprite ? sprite! : defaultEmptyThumbnail, cts.Token);
            }
            catch (OperationCanceledException)
            {
                currentThumbnailUrl = null;
            }
            catch (Exception)
            {
                currentThumbnailUrl = null;
                await SetThumbnailImageWithAnimationAsync(defaultEmptyThumbnail, cts.Token);
            }
        }
    }
}
