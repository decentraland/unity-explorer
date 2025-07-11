using Cysharp.Threading.Tasks;
using DCL.UI;
using DCL.UI.Communities;
using System;
using System.Threading;

namespace DCL.Chat
{
    public class CommunityChatConversationsToolbarViewItem : ChatConversationsToolbarViewItem
    {
        /// <summary>
        /// Provides the data required to display the community thumbnail.
        /// </summary>
        /// <param name="thumbnailCache">A way to access thumbnail images asynchronously.</param>
        /// <param name="imageUrl">The URL to the thumbnail picture.</param>
        /// <param name="ct">A cancellation token for the thumbnail download operation.</param>
        public void SetThumbnailData(ISpriteCache thumbnailCache, Uri? imageUrl, CancellationToken ct)
        {
            customIcon.gameObject.SetActive(false);
            thumbnailView.SetActive(true);

            if(imageUrl != null)
                thumbnailView.GetComponent<CommunityThumbnailView>().LoadThumbnailAsync(thumbnailCache, imageUrl, ct).Forget();
        }
    }
}
