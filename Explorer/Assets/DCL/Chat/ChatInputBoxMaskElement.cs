using TMPro;
using UnityEngine;

namespace DCL.Chat
{
    public class ChatInputBoxMaskElement : MonoBehaviour
    {
        private const string OFFLINE_MASK = "The user you are trying to message is offline.";
        private const string ONLY_FRIENDS_MASK = "The user you are trying to message only accepts DMs from friends.";
        private const string BLOCKED_BY_OWN_MASK = "To message this user you must first unblock them.";
        private const string ONLY_FRIENDS_OWN_MASK = "Add this user as a friend to chat, or update your <b><u>DM settings</b></u> to connect with everyone.";

        [SerializeField] private TMP_Text maskText;

        public void SetUpWithUserState(ChatUserStateUpdater.ChatUserState userState)
        {
            //depending on state we change the text.
            switch (userState)
            {
                case ChatUserStateUpdater.ChatUserState.BLOCKED_BY_OWN_USER:
                    this.maskText.SetText(BLOCKED_BY_OWN_MASK);
                    break;
                case ChatUserStateUpdater.ChatUserState.PRIVATE_MESSAGES_BLOCKED_BY_OWN_USER:
                    this.maskText.SetText(ONLY_FRIENDS_OWN_MASK);
                    break;
                case ChatUserStateUpdater.ChatUserState.PRIVATE_MESSAGES_BLOCKED:
                    this.maskText.SetText(ONLY_FRIENDS_MASK);
                    break;
                case ChatUserStateUpdater.ChatUserState.DISCONNECTED:
                    this.maskText.SetText(OFFLINE_MASK);
                    break;
            }
        }

    }
}
