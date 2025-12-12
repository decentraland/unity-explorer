using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using UnityEngine;

namespace DCL.Chat
{
    public class PrivateChatConversationsToolbarViewItem : ChatConversationsToolbarViewItem
    {
        protected override void Start()
        {
            base.Start();
            removeButton.gameObject.SetActive(true);
        }

        public override void BindProfileThumbnail(IReactiveProperty<ProfileThumbnailViewModel.WithColor> viewModel)
        {
            var pictureView = thumbnailView.GetComponent<ProfilePictureView>();
            if (pictureView != null)
            {
                customIcon.gameObject.SetActive(false);
                thumbnailView.SetActive(true);

                pictureView.Bind(viewModel);
            }
        }

        public override void SetPicture(Sprite? sprite, Color color)
        {
            base.SetColor(color);

            customIcon.gameObject.SetActive(false);
            thumbnailView.SetActive(true);

            var pictureView = thumbnailView.GetComponent<ProfilePictureView>();

            bool isLoading = sprite == null;
            pictureView.SetLoadingState(isLoading);

            if (!isLoading)
            {
                pictureView.SetImage(sprite);
            }
        }
    }
}
