using DCL.UI;
using DCL.WebRequests;
using TMPro;
using UnityEngine;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunityResultCardView : MonoBehaviour
    {
        [field: SerializeField] internal TMP_Text communityTitle { get; private set; }
        [field: SerializeField] internal ImageView communityThumbnail { get; private set; }
        [field: SerializeField] internal Sprite defaultCommunitySprite { get; private set; }

        private ImageController imageController;

        public void ConfigureImageController(IWebRequestController webRequestController)
        {
            if (imageController != null)
                return;

            imageController = new ImageController(communityThumbnail, webRequestController);
        }

        public void SetCommunityThumbnail(string imageUrl)
        {
            imageController?.SetImage(defaultCommunitySprite);

            if (!string.IsNullOrEmpty(imageUrl))
                imageController?.RequestImage(imageUrl, hideImageWhileLoading: true);
        }

        public void SetTitle(string title) =>
            communityTitle.text = title;
    }
}
