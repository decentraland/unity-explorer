using DCL.UI;
using DCL.Chat.History;
using DCL.Prefs;
using DCL.Settings.Settings;
using DCL.UI.Profiles.Helpers;
using DCL.UI.Communities;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.GenericContextMenuParameter;
using DCL.UI.ProfileElements;
using DCL.VoiceChat;
using DCL.Web3;
using DCL.Utilities;
using MVC;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Chat
{
    public class ChatTitleBarView : MonoBehaviour
    {
        public delegate void VisibilityChangedDelegate(bool isVisible);
        public delegate void DeleteChatHistoryRequestedDelegate();
        public delegate void ViewCommunityRequestedDelegate();

        public event Action? CloseChatButtonClicked;
        public event Action? CloseMemberListButtonClicked;
        public event Action? HideMemberListButtonClicked;
        public event Action? ShowMemberListButtonClicked;

        public event VisibilityChangedDelegate? ContextMenuVisibilityChanged;
        public event DeleteChatHistoryRequestedDelegate? DeleteChatHistoryRequested;
        public event ViewCommunityRequestedDelegate ViewCommunityRequested;

        [SerializeField] private Button closeChatButton;
        [SerializeField] private Button showMemberListButton;
        [SerializeField] private Button hideMemberListButton;
        [SerializeField] private Button openContextMenuButton;
        [field: SerializeField] public CallButtonView CallButton { get; private set; }
        [field: SerializeField] public CallButtonView CommunitiesCallButton { get; private set; }

        [SerializeField] private TMP_Text chatTitleMemberListNumberText;
        [SerializeField] private TMP_Text memberListTitleMemberListNumberText;
        [SerializeField] private TMP_Text communityMemberListTitleMemberListNumberText;
        [SerializeField] private TMP_Text chatChannelNameNameText;
        [SerializeField] private TMP_Text memberListChannelNameText;

        [SerializeField] private GameObject defaultChatTitlebar;
        [SerializeField] private GameObject nearbyMemberListTitlebar;
        [SerializeField] private GameObject communitiesMemberListTitlebar;
        [SerializeField] private TMP_Text communitiesMemberListTitlebarText;

        [SerializeField] private GameObject memberCountObject;
        [SerializeField] private GameObject nearbyChannelContainer;
        [SerializeField] private SimpleProfileView profileView;
        [SerializeField] private CommunityTitleView communityChannelContainer;

        [SerializeField] private ChatContextMenuConfiguration chatContextMenuSettings;

        private CancellationTokenSource cts;
        private CancellationTokenSource contextMenuCts;
        private bool isInitialized;
        private GenericContextMenu? contextMenuInstance;
        private ToggleWithCheckContextMenuControlSettings[] notificationPingToggles;
        private ChatChannel.ChannelId currentChannelId;

        /// <summary>
        /// Gets the button that is currently available for folding the chat panel. The titlebar may change depending on whether the Member List is visible or not.
        /// </summary>
        public Button CurrentTitleBarCloseButton
        {
            get
            {
                return closeChatButton;
            }
        }

        public void Initialize()
        {
            if(isInitialized)
                return;

            closeChatButton.onClick.AddListener(OnCloseChatButtonClicked);
            showMemberListButton.onClick.AddListener(OnShowMemberListButtonClicked);
            hideMemberListButton.onClick.AddListener(OnHideMemberListButtonClicked);
            openContextMenuButton.onClick.AddListener(OnOpenContextMenuButtonClicked);
            profileView.ProfileContextMenuOpened += OnProfileContextMenuOpened;
            profileView.ProfileContextMenuClosed += OnProfileContextMenuClosed;
            communityChannelContainer.ContextMenuOpened += OnProfileContextMenuOpened;
            communityChannelContainer.ContextMenuClosed += OnProfileContextMenuClosed;
            communityChannelContainer.ViewCommunityRequested += OnCommunityContextMenuViewCommunityRequested;
            isInitialized = true;
        }

        public void ChangeTitleBarVisibility(bool isMemberListVisible, ChatChannel.ChatChannelType channelType)
        {
            defaultChatTitlebar.SetActive(!isMemberListVisible);
            hideMemberListButton.gameObject.SetActive(isMemberListVisible);

            if (channelType == ChatChannel.ChatChannelType.NEARBY)
            {
                nearbyMemberListTitlebar.SetActive(isMemberListVisible);
            }
            else if (channelType == ChatChannel.ChatChannelType.COMMUNITY)
            {
                communitiesMemberListTitlebar.SetActive(isMemberListVisible);
            }
            else
            {
                nearbyMemberListTitlebar.SetActive(false);
                communitiesMemberListTitlebar.SetActive(false);
            }
        }

        public void SetMemberListNumberText(string userAmount)
        {
            chatTitleMemberListNumberText.text = userAmount;

            if (communityChannelContainer.gameObject.activeInHierarchy)
            {
                communityMemberListTitleMemberListNumberText.text = userAmount;
            }
            else if (nearbyMemberListTitlebar.gameObject.activeInHierarchy)
            {
                memberListTitleMemberListNumberText.text = userAmount;
            }
        }

        public void SetChannelNameText(string channelName)
        {
            if (chatChannelNameNameText.gameObject.activeInHierarchy)
            {
                chatChannelNameNameText.text = channelName;
            }
            else if (memberListChannelNameText.gameObject.activeInHierarchy)
            {
                memberListChannelNameText.text = channelName;
            }
            else if (communitiesMemberListTitlebarText.gameObject.activeInHierarchy)
            {
                communitiesMemberListTitlebarText.text = channelName;
            }
        }

        public void SetCallButtonStatus(bool isActive)
        {
            // We need this as this method can be called from a background thread
            // specially when coming from a Livekit Participant Update
            CallButton.SetActiveOnMainThread(isActive);
        }

        public void SetNearbyChannelImage()
        {
            nearbyChannelContainer.SetActive(true);
            memberCountObject.SetActive(true);
            profileView.gameObject.SetActive(false);
            communityChannelContainer.gameObject.SetActive(false);
        }

        public void SetupProfileView(Web3Address userId, ProfileRepositoryWrapper profileDataProvider)
        {
            cts = cts.SafeRestart();
            profileView.gameObject.SetActive(true);
            profileView.SetupAsync(userId, profileDataProvider, cts.Token).Forget();
            nearbyChannelContainer.SetActive(false);
            memberCountObject.SetActive(false);
            communityChannelContainer.gameObject.SetActive(false);
        }

        public void SetupCommunityView(ISpriteCache thumbnailCache, string communityId, string communityName, string thumbnailUrl, CommunityTitleView.OpenContextMenuDelegate openContextMenuAction, CancellationToken ct)
        {
            nearbyChannelContainer.SetActive(false);
            memberCountObject.SetActive(true);
            profileView.gameObject.SetActive(false);
            communityChannelContainer.gameObject.SetActive(true);
            communityChannelContainer.SetupAsync(thumbnailCache, communityId, communityName, thumbnailUrl, openContextMenuAction, ct).Forget();
        }

        public void SetConnectionStatus(OnlineStatus status)
        {
            profileView.SetConnectionStatus(status);
        }

        private void OnOpenContextMenuButtonClicked()
        {
            contextMenuCts = contextMenuCts.SafeRestart();
            openContextMenuButton.OnSelect(null);
            ContextMenuVisibilityChanged?.Invoke(true);

            if (contextMenuInstance == null)
                InitializeChannelContextMenu();

            // Initializes the value of the toggles inside the submenu of the Notification Ping
            for (int i = 0; i < notificationPingToggles.Length; ++i)
                notificationPingToggles[i].SetInitialValue(i == (int)ChatUserSettings.GetNotificationPingValuePerChannel(currentChannelId));

            ViewDependencies.ContextMenuOpener.OpenContextMenu(new GenericContextMenuParameter(contextMenuInstance,
                                                                                             openContextMenuButton.transform.position,
                                                                                               actionOnHide: OnContextMenuClosed), contextMenuCts.Token);
        }

        private void OnNotificationPingOptionSelected(ChatAudioSettings selectedMode)
        {
            ChatUserSettings.SetNotificationPintValuePerChannel(selectedMode, currentChannelId);
        }

        private void OnDeleteChatHistoryButtonClicked()
        {
            DeleteChatHistoryRequested?.Invoke();
        }

        private void OnContextMenuClosed()
        {
            ContextMenuVisibilityChanged?.Invoke(false);
        }

        private void OnHideMemberListButtonClicked()
        {
            HideMemberListButtonClicked?.Invoke();
        }

        private void OnShowMemberListButtonClicked()
        {
            ShowMemberListButtonClicked?.Invoke();
        }

        private void OnCloseChatButtonClicked()
        {
            CloseChatButtonClicked?.Invoke();
        }

        private void OnProfileContextMenuClosed()
        {
            ContextMenuVisibilityChanged?.Invoke(false);
        }

        private void OnProfileContextMenuOpened()
        {
            ContextMenuVisibilityChanged?.Invoke(true);
        }

        private void OnDisable()
        {
            cts.SafeCancelAndDispose();
            contextMenuCts.SafeCancelAndDispose();
        }

        private void OnCommunityContextMenuViewCommunityRequested()
        {
            ViewCommunityRequested?.Invoke();
        }

        public void SetCurrentChannel(ChatChannel.ChannelId channelId)
        {
            currentChannelId = channelId;
        }

        private void InitializeChannelContextMenu()
        {
            ToggleGroup toggleGroup = gameObject.AddComponent<ToggleGroup>();
            notificationPingToggles = new ToggleWithCheckContextMenuControlSettings[3];
            ButtonContextMenuControlSettings deleteChatHistoryButton = new ButtonContextMenuControlSettings(chatContextMenuSettings.DeleteChatHistoryText, chatContextMenuSettings.DeleteChatHistorySprite, OnDeleteChatHistoryButtonClicked);
            SubMenuContextMenuButtonSettings subMenuSettings = new SubMenuContextMenuButtonSettings(
                chatContextMenuSettings.NotificationPingText,
                chatContextMenuSettings.NotificationPingSprite,
                new GenericContextMenu(chatContextMenuSettings.ContextMenuWidth,
                                             verticalLayoutPadding: chatContextMenuSettings.VerticalPadding,
                                             elementsSpacing: chatContextMenuSettings.ElementsSpacing,
                                             offsetFromTarget: chatContextMenuSettings.NotificationPingSubMenuOffsetFromTarget)
                                        .AddControl(notificationPingToggles[(int)ChatAudioSettings.ALL] = new ToggleWithCheckContextMenuControlSettings("All Messages", x => OnNotificationPingOptionSelected(ChatAudioSettings.ALL), toggleGroup))
                                        .AddControl(notificationPingToggles[(int)ChatAudioSettings.MENTIONS_ONLY] = new ToggleWithCheckContextMenuControlSettings("Mentions Only", x => OnNotificationPingOptionSelected(ChatAudioSettings.MENTIONS_ONLY), toggleGroup))
                                        .AddControl(notificationPingToggles[(int)ChatAudioSettings.NONE] = new ToggleWithCheckContextMenuControlSettings("None", x => OnNotificationPingOptionSelected(ChatAudioSettings.NONE), toggleGroup)));

            contextMenuInstance = new UI.GenericContextMenuParameter.GenericContextMenu(chatContextMenuSettings.ContextMenuWidth, chatContextMenuSettings.OffsetFromTarget, chatContextMenuSettings.VerticalPadding, chatContextMenuSettings.ElementsSpacing, anchorPoint: ContextMenuOpenDirection.TOP_LEFT)
               .AddControl(subMenuSettings)
               .AddControl(deleteChatHistoryButton);
        }
    }
}
