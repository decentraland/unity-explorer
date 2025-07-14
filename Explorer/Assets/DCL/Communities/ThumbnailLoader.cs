
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

        public async UniTaskVoid LoadCommunityThumbnailAsync(string thumbnailUrl, ImageView thumbnailView, Sprite? defaultThumbnail, CancellationToken ct)
        {
            thumbnailView.ImageColor = Color.clear;
            thumbnailView.SetImage(defaultThumbnail!);
            thumbnailView.IsLoading = true;

            Sprite? loadedSprite = null;

            try
            {
                if (!string.IsNullOrEmpty(thumbnailUrl))
                    loadedSprite = await Cache!.GetSpriteAsync(thumbnailUrl, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.COMMUNITIES); }
            finally
            {
                thumbnailView.IsLoading = false;
            }

            if (loadedSprite != null)
                thumbnailView.SetImage(loadedSprite);

            thumbnailView.ShowImageAnimated();
        }
    }
}
