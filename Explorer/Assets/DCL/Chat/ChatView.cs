using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Settings.Settings;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.RealmNavigation;
using DCL.UI;
using DCL.UI.Communities;
using DCL.VoiceChat;
using DCL.Web3;
using MVC;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Utility;

namespace DCL.Chat
{
    public delegate UniTask GetChannelMembersDelegate(List<ChatUserData> outProfiles, CancellationToken ct);

    // Note: The view never changes any data (chatMessages), that's done by the controller
    public class ChatView : ViewBase, IView, IPointerEnterHandler, IPointerExitHandler, IDisposable
    {
        public delegate void EmojiSelectionVisibilityChangedDelegate(bool isVisible);
        public delegate void FoldingChangedDelegate(bool isUnfolded);
        public delegate void FocusChangedDelegate(bool isFocused);
        public delegate void InputSubmittedDelegate(ChatChannel channel, string s, string origin);
        public delegate void MemberListVisibilityChangedDelegate(bool isVisible);
        public delegate void PointerEventDelegate();
        public delegate void ScrollBottomReachedDelegate();
        public delegate void UnreadMessagesSeparatorViewedDelegate();
        public delegate void CurrentChannelChangedDelegate();
        public delegate void ChannelRemovalRequestedDelegate(ChatChannel.ChannelId channelId);
        public delegate void ConversationSelectedDelegate(ChatChannel.ChannelId channelId);
        public delegate void DeleteChatHistoryRequestedDelegate();
        public delegate void ViewCommunityRequestedDelegate(string communityId);

        [Header("Settings")]
        [Tooltip("The time it takes, in seconds, for the background of the chat window to fade-in/out when hovering with the mouse.")]
        [SerializeField]
        private float BackgroundFadeTime = 0.2f;

        [Tooltip("The time it waits, in seconds, since the scroll view reaches the bottom until the scroll-to-bottom button starts hiding.")]
        [SerializeField]
        private float scrollToBottomButtonTimeBeforeHiding = 2.0f;

        [Tooltip("The time it takes, in seconds, for the scroll-to-bottom button to fade out.")]
        [SerializeField]
        private float scrollToBottomButtonFadeOutDuration = 0.5f;

        [Tooltip("The icon to use for the Nearby conversation.")]
        [SerializeField]
        private Sprite nearbyConversationIcon;

        [Header("UI elements")]
        [SerializeField]
        private ChatInputBoxElement chatInputBox;

        [SerializeField]
        private ChatInputBoxMaskElement inputBoxMask;

        [SerializeField]
        private Image unfoldedPanelInteractableArea;

        [Header("Messages")]

        [SerializeField]
        private ChatMessageViewerElement chatMessageViewer;

        [SerializeField]
        private CanvasGroup messagesPanelBackgroundCanvasGroup;

        [SerializeField]
        private GameObject messagesPanel;

        [SerializeField]
        private GameObject chatAndConversationsPanel;

        [SerializeField]
        private ChatMemberListView memberListView;

        [Header("Title bar")]

        [field: SerializeField]
        public ChatTitleBarView chatTitleBar { get; private set; }

        [SerializeField]
        private CanvasGroup titlebarCanvasGroup;

        [Header("Scroll to bottom")]

        [SerializeField]
        private Button scrollToBottomButton;

        [SerializeField]
        private TMP_Text scrollToBottomNumberText;

        [SerializeField]
        private CanvasGroup scrollToBottomCanvasGroup;

        [Header("Conversations toolbar")]

        [SerializeField]
        private ChatConversationsToolbarView conversationsToolbar;

        [SerializeField]
        private CanvasGroup conversationsToolbarCanvasGroup;

        [field: Header("Audio")]
        [field: SerializeField] public AudioClipConfig ChatReceiveMessageAudio { get; private set; }
        [field: SerializeField] public AudioClipConfig ChatReceiveMentionMessageAudio {get; private set;}

        [field: Header("Voice Chat")]
        [field: SerializeField] public CommunityStreamSubTitleBarView CommunityStreamSubTitleBar { get; private set; }

        [field: SerializeField] public CanvasGroup CommunitySubTitleBarCanvasGroup { get; private set; }

        /// <summary>
        /// Raised when the mouse pointer hovers any part of the chat window.
        /// </summary>
        public event PointerEventDelegate PointerEnter;

        /// <summary>
        /// Raised when the mouse pointer stops hovering the chat window.
        /// </summary>
        public event PointerEventDelegate PointerExit;

        /// <summary>
        /// Raised when either the input box gains the focus or loses it.
        /// </summary>
        public event FocusChangedDelegate FocusChanged;

        /// <summary>
        /// Raised when either the emoji selection panel opens or closes.
        /// </summary>
        public event EmojiSelectionVisibilityChangedDelegate EmojiSelectionVisibilityChanged;

        /// <summary>
        /// Raised whenever the user attempts to send the content of the input box as a chat message.
        /// </summary>
        public event InputSubmittedDelegate InputSubmitted;

        /// <summary>
        /// Raised when the user scrolls down the list to the bottom.
        /// </summary>
        public event ScrollBottomReachedDelegate ScrollBottomReached;

        /// <summary>
        /// Raised when the UI is folded or unfolded.
        /// </summary>
        public event FoldingChangedDelegate FoldingChanged;

        /// <summary>
        /// Raised when the Unread messages separator is visible for the user.
        /// </summary>
        public event UnreadMessagesSeparatorViewedDelegate UnreadMessagesSeparatorViewed;

        /// <summary>
        /// Raised when the Member list panel changes its visibility (which implies that the message list may appear or hide).
        /// </summary>
        public event MemberListVisibilityChangedDelegate MemberListVisibilityChanged;

        /// <summary>
        /// Raised when a different conversation is displayed and all messages are replaced.
        /// </summary>
        public event CurrentChannelChangedDelegate CurrentChannelChanged;

        /// <summary>
        /// Raised when the user requests the removal of a channel. Data has not been modified yet and UI has not reacted either.
        /// </summary>
        public event ChannelRemovalRequestedDelegate ChannelRemovalRequested;

        /// <summary>
        /// Raised when trying to open an existing conversation.
        /// </summary>
        public event ConversationSelectedDelegate ConversationSelected;

        /// <summary>
        /// Raised when the user wants to delete the chat history of the current conversation.
        /// </summary>
        public event DeleteChatHistoryRequestedDelegate? DeleteChatHistoryRequested;

        /// <summary>
        /// Raised when the user wants to see the community card for the current conversation.
        /// </summary>
        public event ViewCommunityRequestedDelegate ViewCommunityRequested;

        private ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly List<ChatUserData> sortedMemberData = new ();

        private IReadOnlyDictionary<ChatChannel.ChannelId, ChatChannel>? channels;
        private UniTaskCompletionSource closePopupTask;
        private ChatChannel? currentChannel;

        // The latest amount of messages added to the chat that must be animated yet
        private int entriesPendingToAnimate;
        private CancellationTokenSource fadeoutCts;

        private bool isMemberListCountDirty;
        private bool isMemberListDirty; // These flags are necessary in order to allow the UI respond to state changes that happen in other threads
        private int memberListCount;
        private bool isChatContextMenuOpen;
        private bool isChatViewerMessageContextMenuOpen;
        private CancellationTokenSource popupCts;
        private bool pointerExit;
        private ILoadingStatus loadingStatus;
        private GameObject chatInputBoxGameObject;
        private GameObject inputMaskGameObject;
        private bool isChatFocused;
        private bool isChatUnfolded;
        private bool isPointerOverChat;
        private bool isSubmitHooked;
        private CancellationTokenSource privateConversationItemCts = new CancellationTokenSource();
        private CancellationTokenSource communityConversationItemCts = new CancellationTokenSource();
        private CancellationTokenSource communityTitleCts;

        private ISpriteCache thumbnailCache;
        private CommunityTitleView.OpenContextMenuDelegate openContextMenuAction;
        private Dictionary<ChatChannel.ChannelId, GetUserCommunitiesData.CommunityData> communitiesData = new ();

        public event Action OnCloseButtonClicked = () => { };
        public event Action OnInputButtonClicked = () => { };

        /// <summary>
        /// Get or sets the current content of the input box.
        /// </summary>
        public string InputBoxText
        {
            //TODO FRAN: This should not exist at all ever
            get => chatInputBox.InputBoxText;
            set => chatInputBox.InputBoxText = value;
        }

        /// <summary>
        /// Gets whether the scroll view is showing the bottom of the content, and it can't scroll down anymore.
        /// </summary>
        public bool IsScrollAtBottom => chatMessageViewer.IsScrollAtBottom;

        /// <summary>
        /// Gets or sets the amount of participants in the current channel.
        /// The UI will be refreshed in the next Update.
        /// </summary>
        public int MemberCount
        {
            get => memberListCount;

            set
            {
                if (memberListCount != value)
                {
                    isMemberListCountDirty = true;
                    memberListCount = value;
                }
            }
        }

        /// <summary>
        /// Gets whether the message list panel is visible or not (if the chat is folded, it is considered not visible).
        /// </summary>
        public bool IsMessageListVisible => chatMessageViewer.IsVisible && IsUnfolded;

        /// <summary>
        /// Gets or sets the chat channel to be displayed, using its Id.
        /// </summary>
        public ChatChannel.ChannelId CurrentChannelId
        {
            get => currentChannel!.Id;

            set
            {
                if (currentChannel == null || !currentChannel.Id.Equals(value))
                {
                    currentChannel = channels![value];
                    chatMessageViewer.SetData(currentChannel.Messages);
                    ShowNewMessages();
                    conversationsToolbar.SelectConversation(value);
                    chatInputBox.InputBoxText = string.Empty;
                    memberListView.IsVisible = false;

                    switch (currentChannel.ChannelType)
                    {
                        case ChatChannel.ChatChannelType.NEARBY:
                            SetupViewWithUserState(ChatUserStateUpdater.ChatUserState.CONNECTED);
                            chatTitleBar.SetNearbyChannelImage();
                            break;
                        case ChatChannel.ChatChannelType.USER:
                            chatTitleBar.SetupProfileView(new Web3Address(currentChannel.Id.Id), profileRepositoryWrapper);
                            break;
                        case ChatChannel.ChatChannelType.COMMUNITY:
                            SetupViewWithUserState(ChatUserStateUpdater.ChatUserState.CONNECTED);
                            GetUserCommunitiesData.CommunityData communityData = communitiesData[currentChannel.Id];
                            communityTitleCts = communityTitleCts.SafeRestart();
                            chatTitleBar.SetupCommunityView(thumbnailCache, currentChannel.Id.Id, communityData.name, communityData.thumbnails != null ? communityData.thumbnails.Value.raw : null, openContextMenuAction, communityTitleCts.Token);
                            break;
                    }

                    CurrentChannelChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// Gets whether the member list panel is visible or not (if the chat is unfolded, it is considered not visible):
        /// </summary>
        public bool IsMemberListVisible => memberListView.IsVisible && IsUnfolded;

        [FormerlySerializedAs("isMaskActive")] public bool IsMaskActive;

        /// <summary>
        /// Gets or sets whether the chat panel is open or close (the input box is visible in any case).
        /// But the chat messages will be hidden when folded
        /// </summary>
        public bool IsUnfolded
        {
            get => isChatUnfolded;

            set
            {
                if (value == isChatUnfolded)
                    return;

                memberListView.IsVisible = false;

                unfoldedPanelInteractableArea.enabled = value;
                chatMessageViewer.IsVisible = value;
                SetChatVisibility(value);
                SetBackgroundVisibility(value, false);

                isChatUnfolded = value;

                if (value)
                {
                    ShowNewMessages();
                }
                else
                {
                    Blur();
                    chatMessageViewer.HideSeparator();
                    SetScrollToBottomVisibility(false);
                }

                FoldingChanged?.Invoke(value);
            }
        }

        public void UpdateConversationStatusIconForUser(string userId, OnlineStatus status)
        {
            UpdateStatusIconForChannelAsync(new ChatChannel.ChannelId(userId), status).Forget();
        }

        private async UniTaskVoid UpdateStatusIconForChannelAsync(ChatChannel.ChannelId channelId, OnlineStatus status)
        {
            await UniTask.SwitchToMainThread();

            conversationsToolbar.SetConnectionStatus(channelId, status);

            if(currentChannel != null
               && currentChannel.ChannelType == ChatChannel.ChatChannelType.USER
               && currentChannel.Id.Id == channelId.Id)
                SetTitleBarUserConnectionStatus(status);
        }

        public void SetupInitialConversationToolbarStatusIconForUsers(HashSet<string> userIds)
        {
            foreach (var channelPair in channels)
            {
                if (channelPair.Value.ChannelType == ChatChannel.ChatChannelType.USER)
                {
                    conversationsToolbar.SetConnectionStatus(channelPair.Value.Id,
                        userIds.Contains(channelPair.Value.Id.Id) ?
                        OnlineStatus.ONLINE :
                        OnlineStatus.OFFLINE);
                }

            }

            if (currentChannel!.ChannelType == ChatChannel.ChatChannelType.USER)
            {
                SetTitleBarUserConnectionStatus(userIds.Contains(currentChannel.Id.Id) ?
                    OnlineStatus.ONLINE :
                    OnlineStatus.OFFLINE);
            }
        }

        public void SetProfileDataPovider(ProfileRepositoryWrapper profileDataProvider)
        {
            conversationsToolbar.SetProfileDataProvider(profileDataProvider);
            memberListView.SetProfileDataProvider(profileDataProvider);
            chatMessageViewer.SetProfileDataProvider(profileDataProvider);
            chatInputBox.SetProfileDataProvider(profileDataProvider);
            this.profileRepositoryWrapper = profileDataProvider;
        }

        private void Start()
        {
            IsUnfolded = true;
            SetBackgroundVisibility(false, false);
        }

        private void Awake()
        {
            inputMaskGameObject = inputBoxMask.gameObject;
            chatInputBoxGameObject = chatInputBox.gameObject;
        }

        private void Update()
        {
            // Applies to the UI the data changes performed previously
            if (isMemberListDirty && IsMemberListVisible)
            {
                // In the nearby channel, members are presented alphabetically
                sortedMemberData.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                memberListView.SetData(sortedMemberData);
                memberListCount = sortedMemberData.Count; // The amount of members must match the amount of items in the list
            }

            if (isMemberListDirty || (isMemberListCountDirty && !IsMemberListVisible)) // Once the member list is visible, the number does not change
            {
                chatTitleBar.SetMemberListNumberText(memberListCount.ToString());
            }

            isMemberListDirty = false;
            isMemberListCountDirty = false;
        }


        public void Dispose()
        {
            chatInputBox.Dispose();
            fadeoutCts.SafeCancelAndDispose();
            popupCts.SafeCancelAndDispose();
            privateConversationItemCts.SafeCancelAndDispose();
            communityTitleCts.SafeCancelAndDispose();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            PointerEnter?.Invoke();

            isPointerOverChat = true;

            if (IsUnfolded)
                SetChatVisibility(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // When hovering a context menu, it considers that the mouse is not on the chat, it's a false positive
            if(isChatContextMenuOpen || isChatViewerMessageContextMenuOpen || chatInputBox.IsPasteMenuOpen || chatInputBox.IsEmojiPanelVisible)
                return;

            isPointerOverChat = false;

            PointerExit?.Invoke();

            if (IsUnfolded && !isChatFocused)
                SetChatVisibility(false);
        }

        public override UniTask HideAsync(CancellationToken ct, bool isInstant = false)
        {
            closePopupTask.TrySetResult();
            chatInputBox.ClosePopups();

            chatTitleBar.CloseChatButtonClicked -= OnCloseChatButtonClicked;
            chatTitleBar.CloseMemberListButtonClicked -= OnCloseChatButtonClicked;
            chatTitleBar.ShowMemberListButtonClicked -= OnMemberListOpeningButtonClicked;
            chatTitleBar.HideMemberListButtonClicked -= OnMemberListClosingButtonClicked;
            chatTitleBar.ContextMenuVisibilityChanged -= OnChatContextMenuVisibilityChanged;
            chatTitleBar.DeleteChatHistoryRequested -= OnDeleteChatHistoryRequested;
            chatTitleBar.ViewCommunityRequested -= OnTitleBarViewCommunityRequested;

            chatMessageViewer.ChatMessageOptionsButtonClicked -= OnChatMessageOptionsButtonClickedAsync;
            chatMessageViewer.ChatMessageViewerScrollPositionChanged -= OnChatMessageViewerScrollPositionChanged;
            scrollToBottomButton.onClick.RemoveListener(OnScrollToEndButtonClicked);

            conversationsToolbar.ConversationSelected -= OnConversationsToolbarConversationSelected;
            conversationsToolbar.ConversationRemovalRequested -= OnConversationsToolbarConversationRemovalRequested;

            loadingStatus.CurrentStage.OnUpdate -= SetInputFieldInteractable;
            memberListView.VisibilityChanged -= OnMemberListViewVisibilityChanged;
            chatInputBox.InputBoxFocusChanged -= OnInputBoxSelectionChanged;
            chatInputBox.EmojiSelectionVisibilityChanged -= OnEmojiSelectionVisibilityChanged;
            chatInputBox.InputChanged -= OnInputChanged;
            chatInputBox.InputSubmitted -= OnInputSubmitted;

            DCLInput.Instance.UI.Click.performed -= OnClickUIInputPerformed;
            DCLInput.Instance.UI.Close.performed -= OnCloseUIInputPerformed;
            return base.HideAsync(ct, isInstant);
        }

        /// <summary>
        /// Provides the view a reference to the data it needs to draw the information in the UI, before the community conversations are added.
        /// </summary>
        /// <param name="communities">The data of the communities.</param>
        public void SetCommunitiesData(Dictionary<ChatChannel.ChannelId, GetUserCommunitiesData.CommunityData> communities)
        {
            communitiesData = communities;
        }

        public void Initialize(IReadOnlyDictionary<ChatChannel.ChannelId, ChatChannel> chatChannels,
            ChatSettingsAsset chatSettings,
            GetChannelMembersDelegate getParticipantProfilesDelegate,
            ILoadingStatus loadingStatus,
            IProfileCache profileCache,
            ISpriteCache thumbnailCache,
            CommunityTitleView.OpenContextMenuDelegate openContextMenuAction)
        {
            this.thumbnailCache = thumbnailCache;
            this.openContextMenuAction = openContextMenuAction;
            channels = chatChannels;

            chatTitleBar.Initialize();
            chatTitleBar.CloseChatButtonClicked += OnCloseChatButtonClicked;
            chatTitleBar.CloseMemberListButtonClicked += OnCloseChatButtonClicked;
            chatTitleBar.ShowMemberListButtonClicked += OnMemberListOpeningButtonClicked;
            chatTitleBar.HideMemberListButtonClicked += OnMemberListClosingButtonClicked;
            chatTitleBar.ContextMenuVisibilityChanged += OnChatContextMenuVisibilityChanged;
            chatTitleBar.DeleteChatHistoryRequested += OnDeleteChatHistoryRequested;
            chatTitleBar.ViewCommunityRequested += OnTitleBarViewCommunityRequested;

            this.loadingStatus = loadingStatus;
            loadingStatus.CurrentStage.OnUpdate += SetInputFieldInteractable;

            chatMessageViewer.Initialize();
            chatMessageViewer.ChatMessageOptionsButtonClicked += OnChatMessageOptionsButtonClickedAsync;
            chatMessageViewer.ChatMessageViewerScrollPositionChanged += OnChatMessageViewerScrollPositionChanged;
            scrollToBottomButton.onClick.AddListener(OnScrollToEndButtonClicked);
            memberListView.VisibilityChanged += OnMemberListViewVisibilityChanged;

            chatInputBox.Initialize(chatSettings, getParticipantProfilesDelegate, profileCache);
            chatInputBox.InputBoxFocusChanged += OnInputBoxSelectionChanged;
            chatInputBox.EmojiSelectionVisibilityChanged += OnEmojiSelectionVisibilityChanged;
            chatInputBox.InputChanged += OnInputChanged;
            chatInputBox.InputSubmitted += OnInputSubmitted;

            DCLInput.Instance.UI.Close.performed += OnCloseUIInputPerformed;
            DCLInput.Instance.UI.Click.performed += OnClickUIInputPerformed;
            SubscribeToSubmitEvent();

            closePopupTask = new UniTaskCompletionSource();

            conversationsToolbar.ConversationSelected += OnConversationsToolbarConversationSelected;
            conversationsToolbar.ConversationRemovalRequested += OnConversationsToolbarConversationRemovalRequested;

            // Initializes the conversations toolbar
            foreach (KeyValuePair<ChatChannel.ChannelId, ChatChannel> channelPair in channels)
            {
                if(channelPair.Value.ChannelType == ChatChannel.ChatChannelType.NEARBY)
                    AddNearbyConversation(channelPair.Value);
                else if(channelPair.Value.ChannelType == ChatChannel.ChatChannelType.USER)
                    AddPrivateConversation(channelPair.Value);
            }
        }

        private void OnTitleBarViewCommunityRequested()
        {
            ViewCommunityRequested?.Invoke(communitiesData[CurrentChannelId].id);
        }

        private void OnDeleteChatHistoryRequested()
        {
            DeleteChatHistoryRequested?.Invoke();
        }

        private void SetInputFieldInteractable(LoadingStatus.LoadingStage status)
        {
            if(status == LoadingStatus.LoadingStage.Completed)
                chatInputBox.EnableInputBoxSubmissions();
            else
                chatInputBox.DisableInputBoxSubmissions();
        }

        private void OnScrollToEndButtonClicked()
        {
            chatMessageViewer.ShowLastMessage(true);
        }

        /// <summary>
        /// Makes the chat panel gain the focus so the entire panel will be visible. If there is no mask in the input box, it will be focused.
        /// </summary>
        /// <param name="newText">Optional. It replaces the content of the input box.</param>
        public void Focus(string? newText = null)
        {
            if (!isChatFocused)
            {
                isChatFocused = true;

                chatInputBox.LockSelectedState = true; // This prevents the input box from flickering when clicking on the panel

                if (!memberListView.IsVisible)
                {
                    SetChatVisibility(true);
                    chatInputBox.EnableInputBoxSubmissions();

                    if (IsMaskActive)
                    {
                        inputBoxMask.gameObject.SetActive(true);
                        chatInputBox.gameObject.SetActive(false);
                    }
                    else
                        chatInputBox.Focus(newText);
                }

                FocusChanged?.Invoke(true);
            }
        }

        public void Blur()
        {
            if (isChatFocused)
            {
                isChatFocused = false;

                if(IsMemberListVisible)
                    memberListView.IsVisible = false;

                chatInputBox.LockSelectedState = false;

                if(!isPointerOverChat)
                    SetBackgroundVisibility(false, true);

                inputBoxMask.gameObject.SetActive(false);
                chatInputBox.gameObject.SetActive(true);

                chatInputBox.Blur();
                chatInputBox.DisableInputBoxSubmissions();

                FocusChanged?.Invoke(false);
            }
        }

        /// <summary>
        /// Makes the chat submit the current content of the input box.
        /// </summary>
        public void SubmitInput()
        {
            chatInputBox.SubmitInput();
        }

        /// <summary>
        /// Inserts the sent text at the caret position
        /// </summary>
        /// <param name="text"> the text to be inserted </param>
        public void InsertTextInInputBox(string text)
        {
            chatInputBox.InsertTextAtCaretPosition(text);
        }

#region Messages

        /// <summary>
        /// Makes sure the chat window is showing all the messages stored in the data for the current channel.
        /// </summary>
        public void RefreshMessages()
        {
            int pendingMessages = currentChannel!.Messages.Count - currentChannel.ReadMessages;

            if (pendingMessages > 0)
                chatMessageViewer.ShowSeparator(pendingMessages + 1);

            chatMessageViewer.RefreshMessages();

            SetScrollToBottomVisibility(IsUnfolded && !IsScrollAtBottom && pendingMessages != 0, true);

            if (pendingMessages > 0)
                scrollToBottomNumberText.text = pendingMessages > 9 ? "+9" : pendingMessages.ToString();

            RefreshUnreadMessages(CurrentChannelId);
        }

        /// <summary>
        /// Empties the current conversation removing all messages and hiding associated UI elements.
        /// </summary>
        public void ClearCurrentConversation()
        {
            chatMessageViewer.ClearMessages();
            SetScrollToBottomVisibility(false,false);
            chatMessageViewer.HideSeparator();
        }

        /// <summary>
        /// Moves the chat so it shows the last created message.
        /// </summary>
        public void ShowLastMessage() =>
            chatMessageViewer.ShowLastMessage();

        /// <summary>
        /// Refreshes the list of messages (adds the unread messages elements if needed) and scrolls the list so the first of
        /// the unread messages, if any, is visible.
        /// </summary>
        public void ShowNewMessages()
        {
            if (currentChannel == null) return;

            if (currentChannel!.Messages.Count == 0) return;

            // If there are unread messages...
            if (currentChannel.ReadMessages < currentChannel.Messages.Count)
            {
                // Trick: This is necessary in order to properly refresh the scroll view position
                chatMessageViewer.ShowItem(currentChannel.Messages.Count - 1);

                RefreshMessages();

                chatMessageViewer.ShowItem(chatMessageViewer.CurrentSeparatorIndex - 1); // It shows the first of the unread messages at least

                // Corner case: The new line is visible without doing scroll, and is positioned at the top of the message list
                if(IsScrollAtBottom && chatMessageViewer.CurrentSeparatorIndex >= currentChannel.Messages.Count - 2) // -2: There is a padding message at the top of the list, the separator will be beneath it
                    chatMessageViewer.HideSeparator();

                SetScrollToBottomVisibility(!IsScrollAtBottom);

                if (chatMessageViewer.IsScrollAtBottom)
                    ScrollBottomReached?.Invoke();

                // The separator will always be visible when this occurs
                UnreadMessagesSeparatorViewed?.Invoke();
            }
            else
                RefreshMessages();
        }

        /// <summary>
        /// Replaces the number of unread messages in an item of the conversations toolbar.
        /// </summary>
        /// <param name="destinationChannel">The Id of the conversation.</param>
        public void RefreshUnreadMessages(ChatChannel.ChannelId destinationChannel)
        {
            int unreadMessages = channels[destinationChannel].Messages.Count - channels[destinationChannel].ReadMessages;
            IReadOnlyList<ChatMessage> messages = channels[destinationChannel].Messages;

            // Checks if there is any mention to the current user among the unread messages
            bool hasMentions = false;

            for (int i = 0; i < unreadMessages; ++i)
            {
                if (messages[i + 1].IsMention) // Note: +1 due to padding
                {
                    hasMentions = true;
                    break;
                }
            }

            conversationsToolbar.SetUnreadMessages(destinationChannel, unreadMessages, hasMentions);
        }
#endregion

        /// <summary>
        /// Replaces the data of the participants in the current channel.
        /// The list will be refreshed during the next Update.
        /// </summary>
        /// <param name="memberData">The data of the members to be displayed in the member list.</param>
        public void SetMemberData(List<ChatUserData> memberData)
        {
            sortedMemberData.Clear();
            sortedMemberData.AddRange(memberData);

            isMemberListDirty = true;
        }

        /// <summary>
        /// Changes the visibility of the scroll-to-bottom button.
        /// </summary>
        /// <param name="isVisible">Whether to make it visible or invisible.</param>
        /// <param name="useAnimation">Whether to use a fading animation or change its visual state immediately.</param>
        public void SetScrollToBottomVisibility(bool isVisible, bool useAnimation = false)
        {
            // Resets animation
            scrollToBottomCanvasGroup.DOKill();

            if (isVisible)
            {
                scrollToBottomCanvasGroup.alpha = 1.0f;
                scrollToBottomButton.gameObject.SetActive(true);
            }
            else
            {
                if (useAnimation)
                    scrollToBottomCanvasGroup.DOFade(0.0f, scrollToBottomButtonFadeOutDuration).SetDelay(scrollToBottomButtonTimeBeforeHiding).OnComplete(() => { scrollToBottomButton.gameObject.SetActive(false); });
                else
                {
                    scrollToBottomCanvasGroup.alpha = 0.0f;
                    scrollToBottomButton.gameObject.SetActive(false);
                }
            }
        }

#region Conversations

        /// <summary>
        /// Creates a new item in the conversation toolbar.
        /// </summary>
        /// <param name="channelToAdd">The channel for which the item will be created.</param>
        public void AddNearbyConversation(ChatChannel channelToAdd)
        {
            conversationsToolbar.AddConversation(channelToAdd);
            conversationsToolbar.SetNearbyConversationData(nearbyConversationIcon);
        }

        /// <summary>
        /// Creates a new item in the conversation toolbar.
        /// </summary>
        /// <param name="channelToAdd">The channel for which the item will be created.</param>
        public void AddPrivateConversation(ChatChannel channelToAdd)
        {
            conversationsToolbar.AddConversation(channelToAdd);
            conversationsToolbar.SetPrivateConversationData(channelToAdd.Id, privateConversationItemCts.Token);
        }

        /// <summary>
        /// Creates a new item in the conversation toolbar.
        /// </summary>
        /// <param name="channelToAdd">The channel for which the item will be created.</param>
        /// <param name="thumbnailCache">A reference to the thumbnail cache to get the icon of the toolbar.</param>
        public void AddCommunityConversation(ChatChannel channelToAdd, ISpriteCache thumbnailCache)
        {
            conversationsToolbar.AddConversation(channelToAdd);
            GetUserCommunitiesData.CommunityData communityData = communitiesData[channelToAdd.Id];
            conversationsToolbar.SetCommunityConversationData(channelToAdd.Id, thumbnailCache, communityData, communityConversationItemCts.Token);
        }

        /// <summary>
        /// Removes an item from the conversations toolbar.
        /// </summary>
        /// <param name="channelToRemove">The Id of the conversation.</param>
        public void RemoveConversation(ChatChannel.ChannelId channelToRemove)
        {
            conversationsToolbar.RemoveConversation(channelToRemove);

            if (currentChannel != null && currentChannel.Id.Equals(channelToRemove))
            {
                CurrentChannelId = ChatChannel.NEARBY_CHANNEL.Id;
            }
        }

        /// <summary>
        /// Removes all items from the conversations toolbar.
        /// </summary>
        public void RemoveAllConversations()
        {
            conversationsToolbar.RemoveAllConversations();
            currentChannel = null;
        }

#endregion

        /// <summary>
        /// Must be called from MainThread or will fail.
        /// </summary>
        /// <param name="userState"></param>
        public void SetupViewWithUserState(ChatUserStateUpdater.ChatUserState userState)
        {
            bool isOtherUserConnected = userState == ChatUserStateUpdater.ChatUserState.CONNECTED;
            IsMaskActive = !isOtherUserConnected;

            chatTitleBar.SetCallButtonStatus(currentChannel.ChannelType is ChatChannel.ChatChannelType.USER);
            chatInputBoxGameObject.SetActive(isOtherUserConnected);
            inputMaskGameObject.SetActive(!isOtherUserConnected);

            if (!isOtherUserConnected)
                inputBoxMask.SetUpWithUserState(userState);
            else if(isChatFocused)
                chatInputBox.Focus();
        }

        private void SetChatVisibility(bool isVisible)
        {
            SetBackgroundVisibility(isVisible, true);

            if(!isVisible)
                isPointerOverChat = false;

            if (isVisible)
                chatMessageViewer.StopChatEntriesFadeout();
            else
                chatMessageViewer.StartChatEntriesFadeout();
        }

        private void SetTitleBarUserConnectionStatus(OnlineStatus status)
        {
            chatTitleBar.SetConnectionStatus(status);
        }

        private void OnSubmitUIInputPerformed(InputAction.CallbackContext obj)
        {
            if (isChatFocused)
            {
                chatInputBox.SubmitInputField();
                chatInputBox.Focus(); // Necessary in order not to hide the caret
            }
            else
            {
                // If the Enter key is pressed while the member list is visible, it is hidden and the chat appears
                if (memberListView.IsVisible)
                    memberListView.IsVisible = false;

                Focus();
            }
        }

        private void OnClickUIInputPerformed(InputAction.CallbackContext callbackContext)
        {
            if(!IsUnfolded)
                return;

            IReadOnlyList<RaycastResult> raycastResults = ViewDependencies.EventSystem.RaycastAll(InputSystem.GetDevice<Mouse>().position.value);
            bool hasClickedOnPanel = false;
            bool hasClickedOnCloseButton = false;
            bool hasClickedOnEmojiPanel = false;

            foreach (RaycastResult result in raycastResults)
            {
                if (result.gameObject == unfoldedPanelInteractableArea.gameObject)
                    hasClickedOnPanel = true;
                else if(result.gameObject == chatTitleBar.CurrentTitleBarCloseButton.gameObject)
                    hasClickedOnCloseButton = true;
                else if(result.gameObject == chatInputBox.EmojiSelectionPanel)
                    hasClickedOnEmojiPanel = true;
            }

            if (!hasClickedOnCloseButton)
            {
                if (hasClickedOnPanel && isPointerOverChat)
                {
                    if (!hasClickedOnEmojiPanel)
                    {
                        Focus();
                        chatInputBox.LockSelectedState = true;
                        chatInputBox.Focus();
                    }

                    chatInputBox.OnClicked(raycastResults);
                }
                else if (chatInputBox.IsEmojiPanelVisible && !hasClickedOnEmojiPanel)
                {
                    Blur();
                    chatInputBox.LockSelectedState = false;
                    SetBackgroundVisibility(false, true);
                }
                else if (!isPointerOverChat && !memberListView.IsContextMenuOpen) // This is necessary to avoid blurring while a context menu is open
                {
                    Blur();
                    SetBackgroundVisibility(false, true);
                }
            }
        }

        private void OnChatContextMenuVisibilityChanged(bool isVisible)
        {
            isChatContextMenuOpen = isVisible;
        }

        private void OnInputBoxSelectionChanged(bool inputBoxSelected)
        {
            // When the input box is selected, the chat must unfold and if there is no mask, the input should be selected
            // the chat itself will be considered focused as well until a click outside the chat is registered
            // If the chat was already unfolded, it will just select the input box if possible
            // While the chat is focused, all unwanted inputs will be blocked
            if (inputBoxSelected)
            {
                if (!IsUnfolded)
                {
                    // NOTE: notify controller to handle opening the chat
                    // NOTE: instead of handling it here in the view
                    OnInputButtonClicked();
                    chatMessageViewer.ShowLastMessage();
                }
            }
        }

        private async void OnChatMessageOptionsButtonClickedAsync(string messageText, ChatEntryView chatEntryView)
        {
            isChatViewerMessageContextMenuOpen = true;

            closePopupTask.TrySetResult();
            closePopupTask = new UniTaskCompletionSource();

            var data = new ChatEntryMenuPopupData(
                chatEntryView.messageBubbleElement.PopupPosition,
                messageText,
                () => chatEntryView.messageBubbleElement.HideOptionsButton(),
                closePopupTask.Task);

            popupCts = popupCts.SafeRestart();
            await ViewDependencies.GlobalUIViews.ShowChatEntryMenuPopupAsync(data, popupCts.Token);

            isChatViewerMessageContextMenuOpen = false;
        }

        private void OnCloseChatButtonClicked()
        {
            popupCts.SafeCancelAndDispose();

            // NOTE: notify controller to handle closing the chat
            // NOTE: instead of handling it here in the view
            OnCloseButtonClicked();
        }

        private void OnInputChanged(string inputText)
        {
            closePopupTask.TrySetResult();
            chatMessageViewer.StopChatEntriesFadeout();
        }

        private void OnEmojiSelectionVisibilityChanged(bool isVisible)
        {
            if(isVisible)
                chatInputBox.LockSelectedState = false;
        }

        private void OnInputSubmitted(string messageToSend, string origin)
        {
            InputSubmitted?.Invoke(currentChannel!, messageToSend, origin);
        }

        private void OnChatMessageViewerScrollPositionChanged(Vector2 scrollPosition)
        {
            if (chatMessageViewer.IsScrollAtBottom && currentChannel!.Messages.Count != 0)
            {
                if (scrollToBottomButton.gameObject.activeInHierarchy)
                    SetScrollToBottomVisibility(false, true);

                ScrollBottomReached?.Invoke();
            }

            if (chatMessageViewer.IsSeparatorVisible && chatMessageViewer.IsItemVisible(chatMessageViewer.CurrentSeparatorIndex))
                UnreadMessagesSeparatorViewed?.Invoke();
        }

        private void OnMemberListClosingButtonClicked()
        {
            memberListView.IsVisible = false;
            chatInputBox.Focus();
        }

        private void OnMemberListOpeningButtonClicked()
        {
            memberListView.IsVisible = true;
        }

        private void OnMemberListViewVisibilityChanged(bool isVisible)
        {
            chatTitleBar.ChangeTitleBarVisibility(isVisible, currentChannel.ChannelType);
            chatInputBox.gameObject.SetActive(!isVisible);
            chatAndConversationsPanel.gameObject.SetActive(!isVisible);
            unfoldedPanelInteractableArea.enabled = !isVisible;

            if (currentChannel.ChannelType == ChatChannel.ChatChannelType.COMMUNITY)
            {
                chatTitleBar.SetChannelNameText(communitiesData[CurrentChannelId].name);
            }

            MemberListVisibilityChanged?.Invoke(isVisible);
        }

        private void SetBackgroundVisibility(bool isVisible, bool useAnimation)
        {
            if(memberListView.IsVisible)
                return;

            chatMessageViewer.SetScrollbarVisibility(isVisible, BackgroundFadeTime);
            messagesPanelBackgroundCanvasGroup.DOKill();
            conversationsToolbarCanvasGroup.DOKill();
            titlebarCanvasGroup.DOKill();
            CommunitySubTitleBarCanvasGroup.DOKill();

            if (useAnimation)
            {
                if (isVisible)
                {
                    messagesPanelBackgroundCanvasGroup.gameObject.SetActive(true);
                    conversationsToolbarCanvasGroup.gameObject.SetActive(true);
                    titlebarCanvasGroup.gameObject.SetActive(true);
                    messagesPanelBackgroundCanvasGroup.DOFade(1, BackgroundFadeTime);
                    conversationsToolbarCanvasGroup.DOFade(1, BackgroundFadeTime);
                    titlebarCanvasGroup.DOFade(1, BackgroundFadeTime);
                    CommunitySubTitleBarCanvasGroup.DOFade(1, BackgroundFadeTime);
                }
                else
                {
                    messagesPanelBackgroundCanvasGroup.DOFade(0, BackgroundFadeTime).OnComplete(() => { SetBackgroundVisibility(false, false); });
                    conversationsToolbarCanvasGroup.DOFade(0, BackgroundFadeTime);
                    titlebarCanvasGroup.DOFade(0, BackgroundFadeTime);
                    CommunitySubTitleBarCanvasGroup.DOFade(0, BackgroundFadeTime);
                }
            }
            else
            {
                messagesPanelBackgroundCanvasGroup.alpha = isVisible ? 1.0f : 0.0f;
                messagesPanelBackgroundCanvasGroup.gameObject.SetActive(isVisible);
                conversationsToolbarCanvasGroup.alpha = isVisible ? 1.0f : 0.0f;
                conversationsToolbarCanvasGroup.gameObject.SetActive(isVisible);
                titlebarCanvasGroup.alpha = isVisible ? 1.0f : 0.0f;
                titlebarCanvasGroup.gameObject.SetActive(isVisible);
                CommunitySubTitleBarCanvasGroup.alpha = isVisible? 1.0f : 0.0f;
            }
        }

        private void OnCloseUIInputPerformed(InputAction.CallbackContext callbackContext)
        {
            if (memberListView.IsVisible)
                OnMemberListClosingButtonClicked();

            Blur();
            ;
        }

        private void OnConversationsToolbarConversationSelected(ChatChannel.ChannelId channelId)
        {
            if (currentChannel == null || !CurrentChannelId.Equals(channelId))
                ConversationSelected?.Invoke(channelId);
        }

        private void OnConversationsToolbarConversationRemovalRequested(ChatChannel.ChannelId channelId)
        {
            ChannelRemovalRequested?.Invoke(channelId);
        }

        public void SubscribeToSubmitEvent()
        {
            if (isSubmitHooked)
            {
                ReportHub.Log(ReportCategory.UNSPECIFIED, "Trying to subscribe to submit event when it was already hooked");
                return;
            }

            DCLInput.Instance.UI.Submit.performed += OnSubmitUIInputPerformed;
            isSubmitHooked = true;
        }

        public void UnsubscribeToSubmitEvent()
        {
            if (!isSubmitHooked)
            {
                ReportHub.Log(ReportCategory.UNSPECIFIED, "Trying to unsubscribe from submit event when it was not hooked");
                return;
            }

            DCLInput.Instance.UI.Submit.performed -= OnSubmitUIInputPerformed;
            isSubmitHooked = false;
        }

        /// <summary>
        /// In the conversation toolbar, the channel is moved to the top beneath the nearby channel.
        /// </summary>
        /// <param name="channelToMove">The channel to be moved.</param>
        public void MoveChannelToTop(ChatChannel.ChannelId channelToMove)
        {
            conversationsToolbar.MoveConversationToPosition(channelToMove, 1);
        }

        /// <summary>
        /// Stores a list of users that are online (so if a user is not in it, it's offline). Visual elements (messages, profile pictures, etc.) of offline users
        /// will be greyed out.
        /// </summary>
        /// <param name="onlineUserAddresses">A list of online user addresses.</param>
        public void SetOnlineUserAddresses(HashSet<string> onlineUserAddresses)
        {
            if (currentChannel is { ChannelType: ChatChannel.ChatChannelType.USER })
                chatTitleBar.SetConnectionStatus(onlineUserAddresses.Contains(currentChannel.Id.Id) ? OnlineStatus.ONLINE
                                                                                                    : OnlineStatus.OFFLINE);

            chatMessageViewer.SetOnlineUserAddresses(onlineUserAddresses);
        }
    }
}
