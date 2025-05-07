using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Settings.Settings;
using DCL.Chat.History;
using DCL.Profiles;
using DCL.RealmNavigation;
using DCL.UI;
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
        public delegate void FoldingChangedDelegate(bool isUnfolded);
        public delegate void InputBoxFocusChangedDelegate(bool hasFocus);
        public delegate void InputSubmittedDelegate(ChatChannel channel, string message, string origin);
        public delegate void EmojiSelectionVisibilityChangedDelegate(bool isVisible);
        public delegate void MemberListVisibilityChangedDelegate(bool isVisible);
        public delegate void ScrollBottomReachedDelegate();
        public delegate void PointerEventDelegate();
        public delegate void ChatBubbleVisibilityChangedDelegate(bool isVisible);
        public delegate void UnreadMessagesSeparatorViewedDelegate();

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

        [Header("UI elements")]
        [FormerlySerializedAs("ChatBubblesToggle")]
        [SerializeField]
        private ToggleView chatBubblesToggle;

        [SerializeField]
        private ChatInputBoxElement chatInputBox;

        [FormerlySerializedAs("PanelBackgroundCanvasGroup")]
        [SerializeField]
        private CanvasGroup panelBackgroundCanvasGroup;

        [FormerlySerializedAs("CloseChatButton")]
        [SerializeField]
        private Button closeChatButton;

        [SerializeField]
        private Button closeMemberListButton;

        [SerializeField]
        private ChatMessageViewerElement chatMessageViewer;

        [SerializeField]
        private Button memberListButton;

        [SerializeField]
        private TMP_Text memberListNumberText;

        [SerializeField]
        private TMP_Text memberListNumberText2;

        [SerializeField]
        private ChatMemberListView memberListView;

        [SerializeField]
        private Button memberListOpeningButton;

        [SerializeField]
        private Button memberListClosingButton;

        [SerializeField]
        private GameObject defaultChatTitlebar;

        [SerializeField]
        private GameObject memberListTitlebar;

        [SerializeField]
        private GameObject chatPanel;

        [SerializeField]
        private Button scrollToBottomButton;

        [SerializeField]
        private TMP_Text scrollToBottomNumberText;

        [SerializeField]
        private CanvasGroup scrollToBottomCanvasGroup;

        [Header("Audio")]
        [SerializeField]private AudioClipConfig chatReceiveMessageAudio;
        [SerializeField] private AudioClipConfig chatReceiveMentionMessageAudio;

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
        public event InputBoxFocusChangedDelegate InputBoxFocusChanged;

        /// <summary>
        /// Raised when either the emoji selection panel opens or closes.
        /// </summary>
        public event EmojiSelectionVisibilityChangedDelegate EmojiSelectionVisibilityChanged;

        /// <summary>
        /// Raised whenever the user attempts to send the content of the input box as a chat message.
        /// </summary>
        public event InputSubmittedDelegate InputSubmitted;

        /// <summary>
        /// Raised when the option to change the visibility of the chat bubbles over the avatar changes its value.
        /// </summary>
        public event ChatBubbleVisibilityChangedDelegate? ChatBubbleVisibilityChanged;

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

        private ViewDependencies viewDependencies;
        private UniTaskCompletionSource closePopupTask;

        // The latest amount of messages added to the chat that must be animated yet
        private int entriesPendingToAnimate;
        private CancellationTokenSource fadeoutCts;
        private CancellationTokenSource popupCts;

        private bool isInputSelected;

        private IReadOnlyDictionary<ChatChannel.ChannelId, ChatChannel>? channels;
        private ChatChannel? currentChannel;

        private readonly List<ChatMemberListView.MemberData> sortedMemberData = new();
        private bool isMemberListDirty; // These flags are necessary in order to allow the UI respond to state changes that happen in other threads
        private bool isMemberListCountDirty;
        private int memberListCount;
        private ILoadingStatus loadingStatus;
        private bool isChatUnfolded;

        /// <summary>
        /// Get or sets the current content of the input box.
        /// </summary>
        public string InputBoxText
        {
            get => chatInputBox.InputBoxText;
            set => chatInputBox.InputBoxText = value;
        }

        /// <summary>
        /// Gets whether the scroll view is showing the bottom of the content, and it can't scroll down anymore.
        /// </summary>
        public bool IsScrollAtBottom => chatMessageViewer.IsScrollAtBottom;

        /// <summary>
        /// Gets or sets whether the field that allows changing the visibility of the chat bubbles is enabled or not.
        /// </summary>
        public bool EnableChatBubblesVisibilityField
        {
            get => chatBubblesToggle.Toggle.interactable;

            set
            {
                if (chatBubblesToggle.Toggle.interactable != value)
                {
                    chatBubblesToggle.Toggle.interactable = value;

                    chatBubblesToggle.IsSoundEnabled = false;
                    chatBubblesToggle.Toggle.isOn = chatBubblesToggle.Toggle.interactable;
                    chatBubblesToggle.IsSoundEnabled = true;
                }
            }
        }

        /// <summary>
        /// Gets whether the Unread messages count (AKA scroll-to-bottom button) is visible or not.
        /// </summary>
        public bool IsScrollToBottomButtonVisible => scrollToBottomButton.gameObject.activeInHierarchy;

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
        public ChatChannel.ChannelId CurrentChannel
        {
            get => currentChannel!.Id;

            set
            {
                if (currentChannel == null || !currentChannel.Id.Equals(value))
                {
                    currentChannel = channels![value];

                    chatMessageViewer.SetData(currentChannel.Messages);
                }
            }
        }

        /// <summary>
        /// Gets whether the member list panel is visible or not (if the chat is unfolded, it is considered not visible):
        /// </summary>
        public bool IsMemberListVisible => memberListView.IsVisible && IsUnfolded;

        /// <summary>
        /// Gets or sets whether the chat panel is open or close (the input box is visible in any case).
        /// </summary>
        public bool IsUnfolded
        {
            get => isChatUnfolded;

            set
            {
                if (value == isChatUnfolded)
                    return;

                memberListView.IsVisible = false;
                panelBackgroundCanvasGroup.gameObject.SetActive(value);
                chatMessageViewer.IsVisible = value;

                isChatUnfolded = value;

                if (!value)
                {
                    chatMessageViewer.HideSeparator();
                    SetScrollToBottomVisibility(false);
                }

                FoldingChanged?.Invoke(value);
            }
        }

        public bool IsFocused { get; private set; }

        private void Start()
        {
            panelBackgroundCanvasGroup.alpha = 0;
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
                memberListNumberText.text = memberListCount.ToString();
                memberListNumberText2.text = memberListNumberText.text;
            }

            isMemberListDirty = false;
            isMemberListCountDirty = false;
        }


        public void Dispose()
        {
            chatInputBox.Dispose();
            fadeoutCts.SafeCancelAndDispose();
            popupCts.SafeCancelAndDispose();

            loadingStatus.CurrentStage.OnUpdate -= SetInputFieldInteractable;
            memberListView.VisibilityChanged -= OnMemberListViewVisibilityChanged;
            chatInputBox.InputBoxSelectionChanged -= OnInputBoxSelectionChanged;
            chatInputBox.EmojiSelectionVisibilityChanged -= OnEmojiSelectionVisibilityChanged;
            chatInputBox.InputChanged -= OnInputChanged;
            chatInputBox.InputSubmitted -= OnInputSubmitted;

            viewDependencies.DclInput.UI.Close.performed -= OnUIClosePerformed;
        }

        public void Initialize(IReadOnlyDictionary<ChatChannel.ChannelId, ChatChannel> chatChannels,
            ChatChannel.ChannelId defaultChannelId,
            bool areChatBubblesVisible,
            ChatAudioSettingsAsset chatAudioSettings,
            GetParticipantProfilesDelegate getParticipantProfilesDelegate,
            ILoadingStatus loadingStatus
        )
        {
            this.loadingStatus = loadingStatus;
            loadingStatus.CurrentStage.OnUpdate += SetInputFieldInteractable;

            this.channels = chatChannels;
            closeChatButton.onClick.AddListener(OnCloseChatButtonClicked);
            closeMemberListButton.onClick.AddListener(OnCloseChatButtonClicked);
            memberListOpeningButton.onClick.AddListener(OnMemberListOpeningButtonClicked);
            memberListClosingButton.onClick.AddListener(OnMemberListClosingButtonClicked);
            chatMessageViewer.Initialize();
            chatMessageViewer.ChatMessageOptionsButtonClicked += OnChatMessageOptionsButtonClicked;
            chatMessageViewer.ChatMessageViewerScrollPositionChanged += OnChatMessageViewerScrollPositionChanged;
            scrollToBottomButton.onClick.AddListener(OnScrollToEndButtonClicked);
            memberListView.VisibilityChanged += OnMemberListViewVisibilityChanged;

            chatBubblesToggle.IsSoundEnabled = false;
            chatBubblesToggle.Toggle.isOn = areChatBubblesVisible;
            chatBubblesToggle.Toggle.onValueChanged.AddListener(OnToggleChatBubblesValueChanged);
            chatBubblesToggle.IsSoundEnabled = true;

            chatInputBox.Initialize(chatAudioSettings, getParticipantProfilesDelegate);
            chatInputBox.InputBoxSelectionChanged += OnInputBoxSelectionChanged;
            chatInputBox.EmojiSelectionVisibilityChanged += OnEmojiSelectionVisibilityChanged;
            chatInputBox.InputChanged += OnInputChanged;
            chatInputBox.InputSubmitted += OnInputSubmitted;

            viewDependencies.DclInput.UI.Close.performed += OnUIClosePerformed;
            closePopupTask = new UniTaskCompletionSource();

            CurrentChannel = defaultChannelId;
        }

        private void SetInputFieldInteractable(LoadingStatus.LoadingStage status)
        {
            if(status == LoadingStatus.LoadingStage.Completed)
                chatInputBox.EnableInputBoxSubmissions();
            else
                chatInputBox.DisableInputBoxSubmissions();
        }

        private void OnUIClosePerformed(InputAction.CallbackContext callbackContext)
        {
            if (memberListView.IsVisible)
                OnMemberListClosingButtonClicked();
        }

        private void OnScrollToEndButtonClicked()
        {
            chatMessageViewer.ShowLastMessage(true);
        }

        /// <summary>
        /// Makes the input box stop receiving user inputs.
        /// </summary>
        public void DisableInputBoxSubmissions()
        {
            IsFocused = false;
            chatInputBox.DisableInputBoxSubmissions();
        }

        /// <summary>
        /// Makes the input box start receiving user inputs.
        /// </summary>
        public void EnableInputBoxSubmissions()
        {
            IsFocused = true;
            chatInputBox.EnableInputBoxSubmissions();
        }

        /// <summary>
        /// Makes the input box gain the focus. It does not modify its content.
        /// </summary>
        public void FocusInputBox()
        {
            memberListView.IsVisible = false; // Pressing enter while member list is visible shows the chat again
            chatInputBox.FocusInputBox();
        }

        /// <summary>
        /// Makes the input box gain the focus and replaces its content.
        /// </summary>
        /// <param name="text">The new content of the input box.</param>
        public void FocusInputBoxWithText(string text)
        {
            if (gameObject.activeInHierarchy)
                chatInputBox.FocusInputBoxWithText(text);
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

        /// <summary>
        /// Makes sure the chat window is showing all the messages stored in the data for the current channel.
        /// </summary>
        public void RefreshMessages()
        {
            int pendingMessages = currentChannel!.Messages.Count - currentChannel.ReadMessages;

            if (pendingMessages > 0)
            {
                chatMessageViewer.ShowSeparator(pendingMessages + 1);
            }

            chatMessageViewer.RefreshMessages();

            SetScrollToBottomVisibility(IsUnfolded && !IsScrollAtBottom && pendingMessages != 0, true);

            if (pendingMessages > 0)
                scrollToBottomNumberText.text = pendingMessages > 9 ? "+9" : pendingMessages.ToString();
        }

        /// <summary>
        /// Performs a click event on the chat window.
        /// </summary>
        public void Click()
        {
            chatInputBox.Click();
        }

        /// <summary>
        /// Plays the sound FX of the chat receiving a new message.
        /// </summary>
        public void PlayMessageReceivedSfx(bool isMention)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(isMention? chatReceiveMentionMessageAudio : chatReceiveMessageAudio);
        }

        /// <summary>
        /// Moves the chat so it shows the last created message.
        /// </summary>
        public void ShowLastMessage()
            => chatMessageViewer.ShowLastMessage();

        public void OnPointerEnter(PointerEventData eventData)
        {
            PointerEnter?.Invoke();
            panelBackgroundCanvasGroup.DOFade(1, BackgroundFadeTime);
            chatMessageViewer.SetScrollbarVisibility(true, BackgroundFadeTime);
            chatMessageViewer.StopChatEntriesFadeout();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            PointerExit?.Invoke();
            panelBackgroundCanvasGroup.DOFade(0, BackgroundFadeTime);
            chatMessageViewer.SetScrollbarVisibility(false, BackgroundFadeTime);
            chatMessageViewer.StartChatEntriesFadeout();
        }

        public override UniTask HideAsync(CancellationToken ct, bool isInstant = false)
        {
            closePopupTask.TrySetResult();
            chatInputBox.ClosePopups();
            return base.HideAsync(ct, isInstant);
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
            chatInputBox.InjectDependencies(dependencies);
            chatMessageViewer.InjectDependencies(dependencies);
            memberListView.InjectDependencies(dependencies);
        }

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
        /// Refreshes the list of messages (adds the unread messages elements if needed) and scrolls the list so the first of
        /// the unread messages, if any, is visible.
        /// </summary>
        public void ShowNewMessages()
        {
            if(currentChannel!.Messages.Count == 0)
                return;

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
                if(useAnimation)
                    scrollToBottomCanvasGroup.DOFade(0.0f, scrollToBottomButtonFadeOutDuration).
                                              SetDelay(scrollToBottomButtonTimeBeforeHiding).
                                              OnComplete(() => { scrollToBottomButton.gameObject.SetActive(false); });
                else
                {
                    scrollToBottomCanvasGroup.alpha = 0.0f;
                    scrollToBottomButton.gameObject.SetActive(false);
                }
            }
        }

        private void OnInputBoxSelectionChanged(bool isSelected)
        {
            if (isSelected)
            {
                if (!IsUnfolded)
                {
                    IsUnfolded = true;
                    chatMessageViewer.ShowLastMessage();
                }

                if (isInputSelected) return;

                isInputSelected = true;
                chatMessageViewer.StopChatEntriesFadeout();
                InputBoxFocusChanged?.Invoke(true);
            }
            else
            {
                isInputSelected = false;
                chatMessageViewer.StartChatEntriesFadeout();
                InputBoxFocusChanged?.Invoke(false);
            }
        }

        private void OnChatMessageOptionsButtonClicked(string messageText, ChatEntryView chatEntryView)
        {
            closePopupTask.TrySetResult();
            closePopupTask = new UniTaskCompletionSource();

            ChatEntryMenuPopupData data = new ChatEntryMenuPopupData(
                chatEntryView.messageBubbleElement.PopupPosition,
                messageText,
                () => chatEntryView.messageBubbleElement.HideOptionsButton(),
                closePopupTask.Task);

            popupCts = popupCts.SafeRestart();
            viewDependencies.GlobalUIViews.ShowChatEntryMenuPopupAsync(data, popupCts.Token).Forget();
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
            EmojiSelectionVisibilityChanged?.Invoke(isVisible);
        }

        private void OnInputSubmitted(string messageToSend, string origin)
        {
            InputSubmitted?.Invoke(currentChannel!, messageToSend, origin);
        }

        private void OnToggleChatBubblesValueChanged(bool isToggled)
        {
            ChatBubbleVisibilityChanged?.Invoke(isToggled);
        }

        private void OnChatMessageViewerScrollPositionChanged(Vector2 scrollPosition)
        {
            if (chatMessageViewer.IsScrollAtBottom && currentChannel!.Messages.Count != 0)
            {
                if (IsScrollToBottomButtonVisible)
                    SetScrollToBottomVisibility(false, true);

                ScrollBottomReached?.Invoke();
            }

            if (chatMessageViewer.IsSeparatorVisible && chatMessageViewer.IsItemVisible(chatMessageViewer.CurrentSeparatorIndex))
                UnreadMessagesSeparatorViewed?.Invoke();
        }

        private void OnMemberListClosingButtonClicked()
        {
            memberListView.IsVisible = false;
        }

        private void OnMemberListOpeningButtonClicked()
        {
            memberListView.IsVisible = true;
        }

        private void OnMemberListViewVisibilityChanged(bool isVisible)
        {
            memberListTitlebar.gameObject.SetActive(isVisible);
            defaultChatTitlebar.gameObject.SetActive(!isVisible);
            chatPanel.SetActive(!isVisible);
            chatInputBox.gameObject.SetActive(!isVisible);

            MemberListVisibilityChanged?.Invoke(isVisible);
        }
    }
}
