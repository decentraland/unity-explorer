using DCL.Chat.ChatViewModels;
using DCL.UI.ProfileElements;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat.ChatViews
{
    public class ChatProfileView : MonoBehaviour
    {
        [SerializeField] private GameObject userOnlineStatusIndicator;
        [SerializeField] public ProfilePictureView userProfilePictureView;
        [SerializeField] public ProfilePictureView communityProfilePictureView;
        [SerializeField] private ChatUsernameView usernameElement;
        [SerializeField] private Button buttonOpenProfile;

        public Button ButtonOpenProfile => buttonOpenProfile;

        public void Setup(ChatTitlebarViewModel model)
        {
            if (model.ViewMode == TitlebarViewMode.DirectMessage)
            {
                userOnlineStatusIndicator.SetActive(true);
                userProfilePictureView.Bind(model.Thumbnail, model.ProfileColor);
                userProfilePictureView.gameObject.SetActive(true);
                communityProfilePictureView.gameObject.SetActive(false);
                
                usernameElement.Setup(
                    model.Username,
                    model.WalletId,
                    model.HasClaimedName,
                    model.ProfileColor
                );
            }
            else if (model.ViewMode == TitlebarViewMode.Community)
            {
                userOnlineStatusIndicator.SetActive(false);
                communityProfilePictureView.Bind(model.Thumbnail, model.ProfileColor);
                userProfilePictureView.gameObject.SetActive(false);
                communityProfilePictureView.gameObject.SetActive(true);
                
                usernameElement.Setup(
                    model.Username,
                    null,
                    false,
                    Color.white
                );
            }
        }

        public void SetConnectionStatus(bool isOnline, float greyOutOpacity)
        {
            if (userOnlineStatusIndicator != null)
            {
                userOnlineStatusIndicator.SetActive(isOnline);
            }

            if (userProfilePictureView != null)
            {
                userProfilePictureView.GreyOut(isOnline ? 0.0f : greyOutOpacity);
            }
        }
    }
}
