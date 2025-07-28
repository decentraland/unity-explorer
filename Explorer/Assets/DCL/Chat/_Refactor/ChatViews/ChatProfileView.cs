using DCL.Chat.ChatViewModels;
using DCL.UI.ProfileElements;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatProfileView : MonoBehaviour
    {
        [SerializeField] private ProfilePictureView profilePictureView;
        [SerializeField] private ChatUsernameView usernameElement;
        [SerializeField] private Button buttonOpenProfile;

        public Button ButtonOpenProfile => buttonOpenProfile;

        public void Setup(ChatTitlebarViewModel model)
        {
            profilePictureView.Bind(model.Thumbnail, model.ProfileColor);
            usernameElement.Setup(model.Username, model.WalletId, model.HasClaimedName,
                model.ProfileColor);
        }
    }
}
