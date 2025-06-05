using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UI;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.UI.Communities
{
    public class CommunityThumbnailView : MonoBehaviour, IDisposable
    {
        [SerializeField] private ImageView thumbnailImageView;
        [SerializeField] private Image thumbnailBackground;
        [SerializeField] private Sprite defaultEmptyThumbnail;

        private CancellationTokenSource? cts;
        private string? currentCommunityId;

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
            thumbnailImageView.SetImage(defaultEmptyThumbnail);
            currentCommunityId = null;
        }

        public async UniTask LoadThumbnailAsync(ISpriteCache thumbnailCache, string imageUrl, string communityId,  CancellationToken ct = default)
        {
            if (communityId.Equals(currentCommunityId)) return;

            cts = ct != default ? cts.SafeRestartLinked(ct) : cts.SafeRestart();
            currentCommunityId = communityId;

            try
            {
                ct.ThrowIfCancellationRequested();

                Sprite? sprite = thumbnailCache.GetCachedSprite(communityId);

                if (sprite != null)
                {
                    thumbnailImageView.SetImage(sprite);
                    SetLoadingState(false);
                    thumbnailImageView.Alpha = 1f;
                    return;
                }

                SetLoadingState(true);
                thumbnailImageView.Alpha = 0f;

                sprite = await thumbnailCache!.GetSpriteAsync(imageUrl, cts.Token);

                if (sprite == null)
                    currentCommunityId = null;

                await SetThumbnailImageWithAnimationAsync(sprite ? sprite! : defaultEmptyThumbnail, cts.Token);
            }
            catch (OperationCanceledException)
            {
                currentCommunityId = null;
            }
            catch (Exception)
            {
                currentCommunityId = null;
                await SetThumbnailImageWithAnimationAsync(defaultEmptyThumbnail, cts.Token);
            }
        }

        private async UniTask SetThumbnailImageWithAnimationAsync(Sprite sprite, CancellationToken ct)
        {
            thumbnailImageView.SetImage(sprite);
            thumbnailImageView.ImageEnabled = true;
            await thumbnailImageView.FadeInAsync(0.5f, ct);
        }
    }
}
