using Cysharp.Threading.Tasks;
using DCL.Profiles;
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

        private ViewDependencies viewDependencies;
        private CancellationTokenSource cts;

        public void Setup(Color userColor, string faceSnapshotUrl, string userId)
        {
            thumbnailBackground.color = userColor;
            LoadThumbnailAsync(faceSnapshotUrl, userId).Forget();
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
            try
            {
                cts = ct != default ? cts.SafeRestartLinked(ct) : cts.SafeRestart();
                thumbnailImageView.IsLoading = true;
                thumbnailImageView.ImageEnabled = false;

                Sprite sprite = await viewDependencies.GetThumbnailAsync(userId, faceSnapshotUrl, cts.Token);

                thumbnailImageView.SetImage(sprite ? sprite : defaultEmptyThumbnail);
                thumbnailImageView.ImageEnabled = true;
            }
            catch (Exception)
            {
                thumbnailImageView.SetImage(defaultEmptyThumbnail);
                thumbnailImageView.ImageEnabled = true;
            }

        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
        }

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
        }
    }
}
