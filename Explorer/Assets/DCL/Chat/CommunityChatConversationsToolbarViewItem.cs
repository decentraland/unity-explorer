using Cysharp.Threading.Tasks;
using DCL.UI;
using DCL.UI.Communities;
using System.Threading;
using UnityEngine;

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
        public void SetThumbnailData(ISpriteCache thumbnailCache, string? imageUrl, CancellationToken ct)
        {
            customIcon.gameObject.SetActive(false);
            thumbnailView.SetActive(true);

            if(imageUrl != null)
                thumbnailView.GetComponent<CommunityThumbnailView>().LoadThumbnailAsync(thumbnailCache, imageUrl, ct).Forget();
        }

        public override void SetPicture(Sprite? sprite, Color color)
        {
            customIcon.gameObject.SetActive(false);
            thumbnailView.SetActive(true);

            var communityView = thumbnailView.GetComponent<CommunityThumbnailView>();
            communityView.SetImage(sprite);
        }

        public void SetLoadedThumbnail(Sprite thumbnail)
        {
            customIcon.gameObject.SetActive(false);
            thumbnailView.SetActive(true);

            var thumbnailComponent = thumbnailView.GetComponent<CommunityThumbnailView>();

            if (thumbnail != null)
            {
                thumbnailComponent.SetImage(thumbnail);
            }
        }
        
        protected override void Start()
        {
            base.Start();
            removeButton.gameObject.SetActive(true);
        }
    }
}
