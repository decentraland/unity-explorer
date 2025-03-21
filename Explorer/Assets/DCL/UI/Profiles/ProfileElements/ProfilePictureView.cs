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
            thumbnailBackground.color = userColor;
            SetUpAsync().Forget();
            return;

            async UniTaskVoid SetUpAsync()
            {
                try { await LoadThumbnailAsync(faceSnapshotUrl, userId); }
                catch (OperationCanceledException) { }
            }
        }

        public async UniTask SetupWithDependenciesAsync(ViewDependencies dependencies, Color userColor, string faceSnapshotUrl, string userId, CancellationToken ct)
        {
            viewDependencies = dependencies;
            thumbnailBackground.color = userColor;
            await LoadThumbnailAsync(faceSnapshotUrl, userId, ct);
        }

        public void SetupWithDependencies(ViewDependencies dependencies, Color userColor, string faceSnapshotUrl, string userId)
        {
            viewDependencies = dependencies;
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
        }

        private async UniTask LoadThumbnailAsync(string faceSnapshotUrl, string userId, CancellationToken ct = default)
        {
            cts = ct != default ? cts.SafeRestartLinked(ct) : cts.SafeRestart();

            if (currentThumbnailUrl == faceSnapshotUrl) return;

            cts = ct != default ? cts.SafeRestartLinked(ct) : cts.SafeRestart();

            if (currentThumbnailUrl == faceSnapshotUrl) return;

            try
            {
                ct.ThrowIfCancellationRequested();

                Sprite? sprite = viewDependencies!.GetProfileThumbnail(userId);

                if (sprite != null && !thumbnailImageView.IsLoading)
                {
                    thumbnailImageView.SetImage(sprite);
                    thumbnailImageView.ImageEnabled = true;
                    thumbnailImageView.IsLoading = false;
                    thumbnailImageView.Alpha = 1f;
                    currentThumbnailUrl = faceSnapshotUrl;
                    return;
                }

                thumbnailImageView.IsLoading = true;
                thumbnailImageView.ImageEnabled = false;
                thumbnailImageView.Alpha = 0f;
                thumbnailImageView.Alpha = 0f;

                sprite = await viewDependencies.GetProfileThumbnailAsync(userId, faceSnapshotUrl, cts.Token);
                sprite = await viewDependencies.GetProfileThumbnailAsync(userId, faceSnapshotUrl, cts.Token);

                currentThumbnailUrl = faceSnapshotUrl;
                thumbnailImageView.SetImage(sprite ? sprite! : defaultEmptyThumbnail);
                currentThumbnailUrl = faceSnapshotUrl;
                thumbnailImageView.SetImage(sprite ? sprite! : defaultEmptyThumbnail);
                thumbnailImageView.ImageEnabled = true;
                await thumbnailImageView.FadeInAsync(0.5f, cts.Token);
                await thumbnailImageView.FadeInAsync(0.5f, cts.Token);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                thumbnailImageView.SetImage(defaultEmptyThumbnail);
                thumbnailImageView.ImageEnabled = true;
                await thumbnailImageView.FadeInAsync(1f, cts.Token);
            }
        }
    }
}
