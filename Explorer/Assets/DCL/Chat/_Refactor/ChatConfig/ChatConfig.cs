using DG.Tweening;
using UnityEngine;

namespace DCL.Chat
{
    //[CreateAssetMenu(fileName = "ChatConfig", menuName = "DCL/Chat/ChatConfig")]
    public class ChatConfig : ScriptableObject
    {
        [SerializeField] private string DCL_SYSTEM_SENDER = "DCL System";

        [field: Header("General")]
        [field: SerializeField]
        public Sprite DefaultProfileThumbnail { get; private set; }

        [field: SerializeField]
        public Sprite DefaultCommunityThumbnail { get; private set; }

        [field: SerializeField]
        public Sprite ClearChatHistoryContextMenuIcon { get; private set; }

        [field: Header("Prefabs")]
        [field: SerializeField]
        public ChatConversationsToolbarViewItem ItemPrefab { get; private set; }

        [field: Header("Nearby Channel Specifics")]
        [field: SerializeField]
        public Sprite NearbyConversationIcon { get; private set; }

        [field: SerializeField]
        public string NearbyConversationName { get; private set; } = "Nearby";

        [field: Header("Animations")]
        [field: Tooltip("The time in seconds it takes for the main panels to fade in/out.")]
        [field: SerializeField]
        public float PanelsFadeDuration { get; private set; } = 0.2f;

        [field: Tooltip("The time in seconds before chat messages are starting to fade out.")]
        [field: SerializeField] [field: Range(0f, 20f)]
        public float chatEntriesWaitBeforeFading { get; private set; } = 10f;

        [field: Tooltip("Chat messages fade out duration in seconds.")]
        [field: SerializeField] [field: Range(0f, 20f)]
        public float chatEntriesFadeTime { get; private set; } = 3f;


        [field: Tooltip("The easing function to use for the panel fade animation.")]
        [field: SerializeField]
        public Ease PanelsFadeEase { get; private set; } = Ease.OutQuad;

        [Tooltip("Context menu text shown when clearing chat history.")]
        public string DeleteChatHistoryContextMenuText = "Delete Chat History";

        [Header("Mask Messages")]
        [Tooltip("Message shown when the other user is offline.")]
        public string UserOfflineMessage = "The user you are trying to message is offline.";

        [Tooltip("Message shown when the other user only accepts DMs from friends.")]
        public string OnlyFriendsMessage = "The user you are trying to message only accepts DMs from friends.";

        [Tooltip("Message shown when you have blocked the other user.")]
        public string BlockedByOwnUserMessage = "To message this user you must first unblock them.";

        [Tooltip("Message shown when your own settings prevent you from sending a DM.")]
        public string OnlyFriendsOwnUserMessage = "Add this user as a friend to chat, or update your <b><u>DM settings</b></u> to connect with everyone.";

        [Tooltip("Message when status is being checked.")]
        public string CheckingUserStatusMessage = "Checking user status...";

        // TODO add sounds here
    }
}
