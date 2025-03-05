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

        public void SetupWithDependencies(ViewDependencies dependencies, Color userColor, string faceSnapshotUrl, string userId)
        {
            viewDependencies = dependencies;
            Setup(userColor, faceSnapshotUrl, userId);
        }

        public void SetupOnlyColor(Color userColor)
        {
            thumbnailBackground.color = userColor;
            thumbnailImageView.SetImage(defaultEmptyThumbnail);
        }

        public void SetDefaultThumbnail()
        {
            thumbnailImageView.SetImage(defaultEmptyThumbnail);
        }

        private async UniTaskVoid LoadThumbnailAsync(string faceSnapshotUrl, string userId)
        {
            try
            {
                cts = cts.SafeRestart();
                thumbnailImageView.IsLoading = true;
                thumbnailImageView.ImageEnabled = false;

                Sprite sprite = await viewDependencies.GetThumbnailAsync(userId, faceSnapshotUrl, cts.Token);

                thumbnailImageView.ImageEnabled = (bool) sprite;

                if (sprite)
                    thumbnailImageView.SetImage(sprite);
            }
            catch (Exception)
            {
                thumbnailImageView.SetImage(defaultEmptyThumbnail);
            }
            finally
            {
                thumbnailImageView.IsLoading = false;
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
