using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.UI;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Communities
{
    public class ThumbnailLoader
    {
        private readonly IDecentralandUrlsSource urlsSource;

        public ISpriteCache? Cache { get; set; }

        public ThumbnailLoader(ISpriteCache? spriteCache, IDecentralandUrlsSource urlsSource)
        {
            this.Cache = spriteCache;
            this.urlsSource = urlsSource;
        }

        /// <summary>
        /// Loads a community thumbnail by constructing the URL from the community ID
        /// and fetching it via KTX/Metamorph service
        /// </summary>
        public UniTaskVoid LoadCommunityThumbnailAsync(
            string? communityId,
            ImageView thumbnailView,
            Sprite? defaultThumbnail,
            CancellationToken ct)
        {
            string? thumbnailUrl = string.IsNullOrEmpty(communityId) ? null : GetCommunityThumbnailUrl(communityId);
            return LoadCommunityThumbnailFromUrlAsync(thumbnailUrl, thumbnailView, defaultThumbnail, ct, true);
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
                    loadedSprite = await Cache!.GetSpriteAsync(thumbnailUrl, useKtx, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.COMMUNITIES); }
            finally { thumbnailView.IsLoading = false; }

            if (loadedSprite != null)
                thumbnailView.SetImage(loadedSprite, true);

            thumbnailView.ShowImageAnimated();
        }

        /// <summary>
        /// Constructs the community thumbnail URL locally based on the community ID
        /// </summary>
        /// <param name="communityId">The community ID</param>
        /// <returns>The constructed thumbnail URL</returns>
        private string GetCommunityThumbnailUrl(string communityId)
        {
            string thumbnailBaseUrl = urlsSource.Url(DecentralandUrl.CommunityThumbnail);
            return string.Format(thumbnailBaseUrl, communityId);
        }
    }
}
