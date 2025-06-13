using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UI.Communities;
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
        /// <param name="communityId">The Id of the community (UUID).</param>
        public void SetThumbnailData(IThumbnailCache thumbnailCache, string imageUrl, string communityId)
        {
            customIcon.gameObject.SetActive(false);
            thumbnailView.SetActive(true);
            thumbnailView.GetComponent<CommunityThumbnailView>().LoadThumbnailAsync(thumbnailCache, imageUrl, communityId, CancellationToken.None).Forget();
        }
    }
}
