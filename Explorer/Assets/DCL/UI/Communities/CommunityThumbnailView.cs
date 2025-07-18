using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.UI.Communities
{
    /// <summary>
    /// A small piece of UI that asynchronously gets the sprite of the thumbnail of a community and shows it.
    /// </summary>
    public class CommunityThumbnailView : MonoBehaviour, IDisposable
    {
        [SerializeField] private ImageView thumbnailImageView;
        [SerializeField] private Image thumbnailBackground;
        [SerializeField] private Sprite defaultEmptyThumbnail;
        [SerializeField] private float fadingDuration = 0.5f;

        private CancellationTokenSource? cts;
        private string? currentCommunityUrl;

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
        }

        public void SetLoadingState(bool isLoading)
        {
            thumbnailImageView.IsLoading = isLoading;
            thumbnailImageView.ImageEnabled = !isLoading;
        }

        public void SetDefaultThumbnail()
        {
            thumbnailImageView.SetImage(defaultEmptyThumbnail, true);
            currentCommunityUrl = null;
        }

        public async UniTask LoadThumbnailAsync(ISpriteCache thumbnailCache, string imageUrl, CancellationToken ct = default)
        {
            if (imageUrl.Equals(currentCommunityUrl)) return;

            cts = ct != default ? cts.SafeRestartLinked(ct) : cts.SafeRestart();
            currentCommunityUrl = imageUrl;

            try
            {
                ct.ThrowIfCancellationRequested();

                Sprite? sprite = thumbnailCache.GetCachedSprite(imageUrl);

                if (sprite != null)
                {
                    thumbnailImageView.SetImage(sprite, true);
                    SetLoadingState(false);
                    thumbnailImageView.Alpha = 1f;
                    return;
                }

                SetLoadingState(true);
                thumbnailImageView.Alpha = 0f;

                sprite = await thumbnailCache!.GetSpriteAsync(imageUrl, cts.Token);

                if (sprite == null)
                    currentCommunityUrl = null;

                await SetThumbnailImageWithAnimationAsync(sprite ? sprite! : defaultEmptyThumbnail, cts.Token);
            }
            catch (OperationCanceledException)
            {
                currentCommunityUrl = null;
            }
            catch (Exception)
            {
                currentCommunityUrl = null;
                await SetThumbnailImageWithAnimationAsync(defaultEmptyThumbnail, cts.Token);
            }
        }

        private async UniTask SetThumbnailImageWithAnimationAsync(Sprite sprite, CancellationToken ct)
        {
            thumbnailImageView.SetImage(sprite, true);
            thumbnailImageView.ImageEnabled = true;
            await thumbnailImageView.FadeInAsync(fadingDuration, ct);
        }
    }
}
