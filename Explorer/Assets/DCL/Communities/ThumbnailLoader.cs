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
        /// Loads a thumbnail from a direct URL (for backwards compatibility and non-community images)
        /// </summary>
        public async UniTaskVoid LoadCommunityThumbnailFromUrlAsync(
            string? thumbnailUrl,
            ImageView thumbnailView,
            Sprite? defaultThumbnail,
            CancellationToken ct,
            bool useKtx)
        {
            thumbnailView.ImageColor = Color.clear;
            thumbnailView.SetImage(defaultThumbnail!, true);
            thumbnailView.IsLoading = true;

            Sprite? loadedSprite = null;

            try
            {
                if (!string.IsNullOrEmpty(thumbnailUrl))
                    loadedSprite = await Cache!.GetSpriteAsync(thumbnailUrl, useKtx, ct: ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.COMMUNITIES); }
            finally { thumbnailView.IsLoading = false; }

            if (loadedSprite != null)
                thumbnailView.SetImage(loadedSprite, true);

            thumbnailView.ShowImageAnimated();
        }
    }
}
