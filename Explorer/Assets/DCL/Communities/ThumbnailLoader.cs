using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.UI;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Communities
{
    public class ThumbnailLoader
    {
        public ISpriteCache? Cache { get; set; }

        public ThumbnailLoader(ISpriteCache? spriteCache)
        {
            this.Cache = spriteCache;
        }

        /// <summary>
        /// Loads a thumbnail from a direct URL (for backwards compatibility and non-community images).
        /// A sprite already cached is shown at once, without the loading look and the fade-in.
        /// </summary>
        public async UniTaskVoid LoadCommunityThumbnailFromUrlAsync(
            string? thumbnailUrl,
            ImageView thumbnailView,
            Sprite? defaultThumbnail,
            CancellationToken ct,
            bool useKtx)
        {
            Sprite? cached = string.IsNullOrEmpty(thumbnailUrl) ? null : Cache!.GetCachedSprite(thumbnailUrl);

            if (cached != null)
            {
                thumbnailView.IsLoading = false;
                thumbnailView.SetImage(cached, true);
                thumbnailView.ImageColor = Color.white;
                return;
            }

            thumbnailView.ImageColor = Color.clear;
            thumbnailView.SetImage(defaultThumbnail!, true);
            thumbnailView.IsLoading = true;

            Sprite? loadedSprite = null;

            try
            {
                if (!string.IsNullOrEmpty(thumbnailUrl))
                    loadedSprite = await Cache!.GetSpriteAsync(thumbnailUrl, useKtx, ct: ct);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.COMMUNITIES); }

            thumbnailView.IsLoading = false;

            if (loadedSprite != null)
                thumbnailView.SetImage(loadedSprite, true);

            thumbnailView.ShowImageAnimated();
        }
    }
}
