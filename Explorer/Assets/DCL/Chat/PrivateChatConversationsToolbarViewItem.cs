using DCL.UI.ProfileElements;
using DCL.Utilities;
using UnityEngine;

namespace DCL.Chat
{
    public class PrivateChatConversationsToolbarViewItem : ChatConversationsToolbarViewItem
    {
        private readonly ReactiveProperty<ProfileThumbnailViewModel> thumbnail = new (ProfileThumbnailViewModel.Default());

        protected override void Start()
        {
            base.Start();
            removeButton.gameObject.SetActive(true);
        }

        public override void BindProfileThumbnail(IReactiveProperty<ProfileThumbnailViewModel> viewModel)
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

            thumbnail.UpdateValue(sprite == null
                ? ProfileThumbnailViewModel.ReadyToLoad()
                : ProfileThumbnailViewModel.FromFallback(sprite));
            pictureView.Bind(thumbnail);
        }
    }
}
