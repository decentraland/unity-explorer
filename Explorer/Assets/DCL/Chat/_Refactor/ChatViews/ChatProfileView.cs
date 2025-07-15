using DCL.UI.ProfileElements;
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

        public void Setup(Sprite sprite, bool isLoading)
        {
            chatProfilePictureView.Setup(sprite, isLoading);
        }

        public void SetProfileBackgroundColor(Color color)
        {
            chatProfilePictureView.SetBackgroundColor(color);
        }
    }
}