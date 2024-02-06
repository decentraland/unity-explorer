using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatEntryView : MonoBehaviour
    {
        [field: SerializeField]
        internal TMP_Text playerName { get; private set; }

        [field: SerializeField]
        internal Image playerIcon { get; private set; }

        [field: SerializeField]
        internal TMP_Text entryText { get; private set; }

        [field: SerializeField]
        internal TMP_Text walletIdText { get; private set; }

        [field: SerializeField]
        internal Image verifiedIcon { get; private set; }

        [field: SerializeField]
        internal HorizontalLayoutGroup layoutGroup { get; private set; }

        [field: SerializeField]
        internal Image entryBackground { get; private set; }

        private ChatEntryConfigurationSO entryConfiguration;

        public void Initialise(ChatEntryConfigurationSO chatEntryConfiguration)
        {
            entryConfiguration = chatEntryConfiguration;
        }

        public void SetUsername(string username, string walletId)
        {
            playerName.text = username;
            walletIdText.text = walletId;
            verifiedIcon.gameObject.SetActive(string.IsNullOrEmpty(walletId));
        }

        public void SetSentByUser(bool sentByUser)
        {
            layoutGroup.reverseArrangement = sentByUser;

            entryBackground.sprite = sentByUser ? entryConfiguration.ownUsersBackground : entryConfiguration.otherUsersBackground;
            entryBackground.color = sentByUser ? entryConfiguration.ownUsersEntryColor : entryConfiguration.otherUsersEntryColor;
            verifiedIcon.sprite = sentByUser ? entryConfiguration.ownUserVerifiedIcon : entryConfiguration.otherUsersVerifiedIcon;
        }
    }
}
