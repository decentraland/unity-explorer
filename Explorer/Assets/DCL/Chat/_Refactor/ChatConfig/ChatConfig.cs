using DCL.Audio;
using DCL.UI.Communities;
using DCL.Utilities;
using DG.Tweening;
using UnityEngine;

namespace DCL.Chat.ChatConfig
{
    //[CreateAssetMenu(fileName = "ChatConfig", menuName = "DCL/Chat/ChatConfig")]
    public class ChatConfig : ScriptableObject
    {
        [SerializeField] private string DCL_SYSTEM_SENDER = "DCL System";

        [SerializeField]
        public CommunityChatConversationContextMenuSettings communityChatConversationContextMenuSettings;

        [SerializeField]
        public ChatContextMenuConfiguration chatContextMenuSettings;

        [field: Header("General")]
        [field: SerializeField]
        public Sprite DefaultProfileThumbnail { get; private set; }

        [field: SerializeField]
        public Sprite DefaultCommunityThumbnail { get; private set; }
        [field: SerializeField]
        public Sprite ClearChatHistoryContextMenuIcon { get; private set; }

        [field: SerializeField]
        public Sprite TranslateChatMessageContextMenuIcon { get; private set; }

        [field: SerializeField]
        public Sprite SeeOriginalChatMessageContextMenuIcon { get; private set; }

        [field: SerializeField]
        public Sprite CopyChatMessageContextMenuIcon { get; private set; }

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

        [Tooltip("Message when input is unfocused.")]
        public string InputUnfocusedMessages = "Press Enter to chat";

        [Tooltip("Message when input is focused but empty.")]
        public string InputFocusedMessages = "Write a message";

        [Tooltip("Profile fetch error message.")]
        public string ProfileFetchErrorMessage = "Couldn't fetch user profile. Please try again later.";

        [field: Header("Translations")]
        [field: SerializeField] public bool ForceEnableTranslations = true;
        [field: SerializeField] public LanguageCode DefaultLanguage = LanguageCode.ES;
        [field: SerializeField] public int TranslationMaxRetries { get; set; } = 1;
        [field: SerializeField] public float TranslationTimeoutSeconds { get; set; } = 10.0f;

        [field: Header("Audio")]
        [field: SerializeField] public AudioClipConfig ChatReceiveMessageAudio { get; private set; }
        [field: SerializeField] public AudioClipConfig ChatReceiveMentionMessageAudio { get; private set; }

        [field: Header("Chat Context Menu")]
        [field: SerializeField] public Vector2 ContextMenuOffset { get; private set; } = new (-220, 100);

        [field: SerializeField] public int ContextMenuWidth { get; private set; } = 218;
        [field: SerializeField] public int ElementsSpacing { get; private set; } = 5;
        [field: SerializeField] public RectOffset VerticalPadding { get; private set; } = null!;
        public string ChatContextMenuCopyText = "Copy";
        public string ChatContextMenuSeeOriginalText = "See Original";
        public string ChatContextMenuTranslateText = "Translate";

    }
}
