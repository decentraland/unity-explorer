using DCL.Chat.ChatViewModels;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatProfileView : MonoBehaviour
    {
        [SerializeField] private ChatProfilePictureView chatProfilePictureView;
        [SerializeField] private ChatUsernameView usernameElement;
        [SerializeField] private Button buttonOpenProfile;

        public Button ButtonOpenProfile => buttonOpenProfile;

        public void Setup(ChatTitlebarViewModel model)
        {
            chatProfilePictureView.Setup(model.ProfileSprite, model.IsLoadingProfile);

            if (model.ViewMode == TitlebarViewMode.DirectMessage)
            {
                usernameElement.Setup(
                    model.Username,
                    model.WalletId,
                    model.HasClaimedName,
                    model.ProfileColor
                );
                SetProfileBackgroundColor(model.ProfileColor);
            }
            else if (model.ViewMode == TitlebarViewMode.Community)
            {
                usernameElement.Setup(
                    model.Username,
                    null,
                    false,
                    Color.white
                );
                SetProfileBackgroundColor(Color.gray);
            }
        }

        public void SetProfileBackgroundColor(Color color)
        {
            chatProfilePictureView.SetBackgroundColor(color);
        }
    }
}