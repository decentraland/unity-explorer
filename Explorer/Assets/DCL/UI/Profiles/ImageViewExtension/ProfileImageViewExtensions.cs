using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Web3;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.UI.Profiles
{
    public static class ProfileImageViewExtensions
    {
        public static async UniTask LoadThumbnailSafeAsync(this ImageView imageView, IProfileThumbnailCache thumbnailCache,
            Web3Address userId, URLAddress thumbnailUrl, CancellationToken ct)
        {
            try
            {
                imageView.IsLoading = true;
                imageView.ImageEnabled = false;

                Sprite? sprite = await thumbnailCache.GetThumbnailAsync(userId, thumbnailUrl, ct);

                imageView.ImageEnabled = sprite != null;

                if (sprite != null)
                    imageView.SetImage(sprite);
            }
            catch (Exception)
            {
                imageView.ImageEnabled = false;
            }
            finally
            {
                imageView.IsLoading = false;
            }
        }
    }
}
