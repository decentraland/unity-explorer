using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Settings.Settings;
using DCL.Chat.History;
using DCL.Profiles;
using DCL.RealmNavigation;
using DCL.UI;
using DCL.Web3;
using MVC;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Utility;

namespace DCL.Chat
{
    public delegate void GetParticipantProfilesDelegate(List<Profile> outProfiles);

    // Note: The view never changes any data (chatMessages), that's done by the controller
    public class ChatView : ViewBase, IView, IViewWithGlobalDependencies, IPointerEnterHandler, IPointerExitHandler, IDisposable
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

        [SerializeField]
        private ChatTitleBarView chatTitleBar;

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

        public event ConversationSelectedDelegate ConversationSelected;

        private ViewDependencies viewDependencies;
        private readonly List<ChatMemberListView.MemberData> sortedMemberData = new ();

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
        /// </ summary>
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
                            SetInputWithUserState(ChatUserStateUpdater.ChatUserState.CONNECTED);
                            chatTitleBar.SetNearbyChannelImage();
                            break;
                        case ChatChannel.ChatChannelType.USER:
                            chatTitleBar.SetupProfileView(new Web3Address(currentChannel.Id.Id));
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

        public void UpdateConversationToolbarStatusIconForUser(string userId, OnlineStatus status)
        {
            foreach (var channelId in channels!.Keys)
            {
                if (channelId.Id.Equals(userId))
                    conversationsToolbar.UpdateConnectionStatusIcon(channelId, status);
            }
        }

        public void SetupInitialConversationToolbarStatusIconForUsers(HashSet<string> userIds)
        {
            foreach (var channelId in channels!.Keys)
            {
                conversationsToolbar.UpdateConnectionStatusIcon(channelId,
                    userIds.Contains(channelId.Id) ?
                    OnlineStatus.ONLINE :
                    OnlineStatus.OFFLINE);
            }
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
            if(isChatContextMenuOpen || isChatViewerMessageContextMenuOpen || chatInputBox.IsPasteMenuOpen)
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

            viewDependencies.DclInput.UI.Click.performed -= OnClickUIInputPerformed;
            viewDependencies.DclInput.UI.Close.performed -= OnCloseUIInputPerformed;
            viewDependencies.DclInput.UI.Submit.performed -= OnSubmitUIInputPerformed;
            return base.HideAsync(ct, isInstant);
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
            chatInputBox.InjectDependencies(dependencies);
            chatMessageViewer.InjectDependencies(dependencies);
            memberListView.InjectDependencies(dependencies);
            chatTitleBar.InjectDependencies(dependencies);
            conversationsToolbar.InjectDependencies(dependencies);
        }

        public void Initialize(IReadOnlyDictionary<ChatChannel.ChannelId, ChatChannel> chatChannels,
            ChatSettingsAsset chatSettings,
            GetParticipantProfilesDelegate getParticipantProfilesDelegate,
            ILoadingStatus loadingStatus)
        {
            channels = chatChannels;

            chatTitleBar.Initialize();
            chatTitleBar.CloseChatButtonClicked += OnCloseChatButtonClicked;
            chatTitleBar.CloseMemberListButtonClicked += OnCloseChatButtonClicked;
            chatTitleBar.ShowMemberListButtonClicked += OnMemberListOpeningButtonClicked;
            chatTitleBar.HideMemberListButtonClicked += OnMemberListClosingButtonClicked;
            chatTitleBar.ContextMenuVisibilityChanged += OnChatContextMenuVisibilityChanged;

            this.loadingStatus = loadingStatus;
            loadingStatus.CurrentStage.OnUpdate += SetInputFieldInteractable;

            chatMessageViewer.Initialize();
            chatMessageViewer.ChatMessageOptionsButtonClicked += OnChatMessageOptionsButtonClickedAsync;
            chatMessageViewer.ChatMessageViewerScrollPositionChanged += OnChatMessageViewerScrollPositionChanged;
            scrollToBottomButton.onClick.AddListener(OnScrollToEndButtonClicked);
            memberListView.VisibilityChanged += OnMemberListViewVisibilityChanged;

            chatInputBox.Initialize(chatSettings, getParticipantProfilesDelegate);
            chatInputBox.InputBoxFocusChanged += OnInputBoxSelectionChanged;
            chatInputBox.EmojiSelectionVisibilityChanged += OnEmojiSelectionVisibilityChanged;
            chatInputBox.InputChanged += OnInputChanged;
            chatInputBox.InputSubmitted += OnInputSubmitted;

            viewDependencies.DclInput.UI.Submit.performed += OnSubmitUIInputPerformed;
            viewDependencies.DclInput.UI.Close.performed += OnCloseUIInputPerformed;
            viewDependencies.DclInput.UI.Click.performed += OnClickUIInputPerformed;

            closePopupTask = new UniTaskCompletionSource();

            conversationsToolbar.ConversationSelected += OnConversationsToolbarConversationSelected;
            conversationsToolbar.ConversationRemovalRequested += OnConversationsToolbarConversationRemovalRequested;

            // Initializes the conversations toolbar
            foreach (KeyValuePair<ChatChannel.ChannelId, ChatChannel> channelPair in channels)
                AddConversation(channelPair.Value);
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

                    if (!IsMaskActive)
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

                chatInputBox.LockSelectedState = false;

                if(!isPointerOverChat)
                    SetBackgroundVisibility(false, true);

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
            conversationsToolbar.SetUnreadMessages(destinationChannel, channels[destinationChannel].Messages.Count - channels[destinationChannel].ReadMessages);
        }
#endregion

        /// <summary>
        /// Replaces the data of the participants in the current channel.
        /// The list will be refreshed during the next Update.
        /// </summary>
        /// <param name="memberData">The data of the members to be displayed in the member list.</param>
        public void SetMemberData(List<ChatMemberListView.MemberData> memberData)
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
        public void AddConversation(ChatChannel channelToAdd)
        {
            if (channelToAdd.Id.Equals(ChatChannel.NEARBY_CHANNEL.Id))
                conversationsToolbar.AddConversation(channelToAdd, nearbyConversationIcon);
            else
                conversationsToolbar.AddConversation(channelToAdd);
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

        public void SetInputWithUserState(ChatUserStateUpdater.ChatUserState userState)
        {
            bool isOtherUserConnected = userState == ChatUserStateUpdater.ChatUserState.CONNECTED;
            chatInputBoxGameObject.SetActive(isOtherUserConnected);

            IsMaskActive = !isOtherUserConnected;
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
            {
                chatMessageViewer.StopChatEntriesFadeout();

                if (IsMaskActive)
                {
                    chatInputBox.gameObject.SetActive(false);
                    inputBoxMask.gameObject.SetActive(true);
                }
            }
            else
            {
                chatMessageViewer.StartChatEntriesFadeout();

                chatInputBox.gameObject.SetActive(true);
                inputBoxMask.gameObject.SetActive(false);
            }
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

            IReadOnlyList<RaycastResult> raycastResults = viewDependencies.EventSystem.RaycastAll(InputSystem.GetDevice<Mouse>().position.value);
            bool hasClickedOnPanel = false;
            bool hasClickedOnCloseButton = false;

            foreach (RaycastResult result in raycastResults)
            {
                if (result.gameObject == unfoldedPanelInteractableArea.gameObject)
                    hasClickedOnPanel = true;
                else if(result.gameObject == chatTitleBar.CurrentTitleBarCloseButton.gameObject)
                    hasClickedOnCloseButton = true;
            }

            if (!hasClickedOnCloseButton)
            {
                if (hasClickedOnPanel)
                {
                    Focus();
                    chatInputBox.OnClicked(raycastResults);
                }
                else if(!isPointerOverChat) // This is necessary to avoid blurring while a context menu is open
                    Blur();
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
                    IsUnfolded = true;
                    Focus();
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
            await viewDependencies.GlobalUIViews.ShowChatEntryMenuPopupAsync(data, popupCts.Token);

            isChatViewerMessageContextMenuOpen = false;
        }

        private void OnCloseChatButtonClicked()
        {
            popupCts.SafeCancelAndDispose();
            IsUnfolded = false;
        }

        private void OnInputChanged(string inputText)
        {
            closePopupTask.TrySetResult();
            chatMessageViewer.StopChatEntriesFadeout();
        }

        private void OnEmojiSelectionVisibilityChanged(bool isVisible)
        {
            // If the user opens the emoji panel by clicking on the button while not focused...
            if(isVisible && !isChatFocused)
                Focus();

            EmojiSelectionVisibilityChanged?.Invoke(isVisible);
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
            chatTitleBar.ChangeTitleBarVisibility(isVisible);
            chatInputBox.gameObject.SetActive(!isVisible);
            chatAndConversationsPanel.gameObject.SetActive(!isVisible);
            unfoldedPanelInteractableArea.enabled = !isVisible;

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
                }
                else
                {
                    messagesPanelBackgroundCanvasGroup.DOFade(0, BackgroundFadeTime).OnComplete(() => { SetBackgroundVisibility(false, false); });
                    conversationsToolbarCanvasGroup.DOFade(0, BackgroundFadeTime);
                    titlebarCanvasGroup.DOFade(0, BackgroundFadeTime);
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
            }
        }

        private void OnCloseUIInputPerformed(InputAction.CallbackContext callbackContext)
        {
            if (memberListView.IsVisible)
                OnMemberListClosingButtonClicked();

            Blur();
        }

        private void OnConversationsToolbarConversationSelected(ChatChannel.ChannelId channelId)
        {
            if (currentChannel == null || !CurrentChannelId.Equals(channelId))
                if (!channelId.Equals(ChatChannel.NEARBY_CHANNEL_ID))
                    ConversationSelected?.Invoke(channelId);
                else
                    CurrentChannelId = channelId;
        }

        private void OnConversationsToolbarConversationRemovalRequested(ChatChannel.ChannelId channelId)
        {
            ChannelRemovalRequested?.Invoke(channelId);
        }
    }
}
