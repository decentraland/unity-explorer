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
            usernameElement.Setup(model.Username, model.WalletId, model.HasClaimedName,
                model.ProfileColor);
        }

        public void SetProfileBackgroundColor(Color color)
        {
            chatProfilePictureView.SetBackgroundColor(color);
        }
    }
}