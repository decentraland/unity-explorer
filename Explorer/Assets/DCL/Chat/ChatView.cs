using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.UI;
using MVC;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Utility;

namespace DCL.Chat
{
    // Note: The view never changes any data (chatMessages), that's done by the controller
    public class ChatView : ViewBase, IView, IViewWithGlobalDependencies, IPointerEnterHandler, IPointerExitHandler, IDisposable
    {
        public delegate Color CalculateUsernameColorDelegate(ChatMessage chatMessage);
        public delegate void InputBoxFocusChangedDelegate(bool hasFocus);
        public delegate void InputSubmittedDelegate(ChatChannel channel, string message, string origin);
        public delegate void EmojiSelectionVisibilityChangedDelegate(bool isVisible);

        [Header("Settings")]
        [Tooltip("The time it takes, in seconds, for the background of the chat window to fade-in/out when hovering with the mouse.")]
        [SerializeField]
        private float BackgroundFadeTime = 0.2f;

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
        private ChatMessageViewerElement chatMessageViewer;

        [Header("Audio")]
        [SerializeField]
        private AudioClipConfig chatReceiveMessageAudio;

        private IReadOnlyList<ChatMessage> chatMessages;

        /// <summary>
        /// Raised when the mouse pointer hovers any part of the chat window.
        /// </summary>
        public event Action PointerEnter;

        /// <summary>
        /// Raised when the mouse pointer stops hovering the chat window.
        /// </summary>
        public event Action PointerExit;

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
        public event Action<bool>? ChatBubbleVisibilityChanged;

        private ViewDependencies viewDependencies;
        private UniTaskCompletionSource closePopupTask;

        // The latest amount of messages added to the chat that must be animated yet
        private int entriesPendingToAnimate;
        private CancellationTokenSource fadeoutCts;

        private bool isChatClosed;
        private bool isInputSelected;

        private IReadOnlyDictionary<ChatChannel.ChannelId, ChatChannel>? channels;
        private ChatChannel? currentChannel;
        private ChatEntryConfigurationSO chatEntryConfiguration;

        /// <summary>
        ///     Get or sets the current content of the input box.
        /// </summary>
        public string InputBoxText
        {
            get => chatInputBox.InputBoxText;
            set => chatInputBox.InputBoxText = value;
        }

        /// <summary>
        ///     Gets or sets whether the field that allows changing the visibility of the chat bubbles is enabled or not.
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

        private void Start()
        {
            panelBackgroundCanvasGroup.alpha = 0;
        }

        public void Dispose()
        {
            chatInputBox.Dispose();
            fadeoutCts.SafeCancelAndDispose();
        }

        public void Initialize(IReadOnlyDictionary<ChatChannel.ChannelId, ChatChannel> chatChannels, ChatChannel.ChannelId defaultChannelId, bool areChatBubblesVisible, ChatEntryConfigurationSO chatEntryConfiguration)
        {
            this.channels = chatChannels;
            this.chatEntryConfiguration = chatEntryConfiguration;
            closeChatButton.onClick.AddListener(CloseChat);
            chatMessageViewer.Initialize(CalculateUsernameColor);
            chatMessageViewer.ChatMessageOptionsButtonClicked += OnChatMessageOptionsButtonClicked;

            chatBubblesToggle.IsSoundEnabled = false;
            chatBubblesToggle.Toggle.isOn = areChatBubblesVisible;
            chatBubblesToggle.Toggle.onValueChanged.AddListener(OnToggleChatBubblesValueChanged);
            chatBubblesToggle.IsSoundEnabled = true;

            chatInputBox.Initialize();
            chatInputBox.InputBoxSelectionChanged += OnInputBoxSelectionChanged;
            chatInputBox.EmojiSelectionVisibilityChanged += OnEmojiSelectionVisibilityChanged;
            chatInputBox.InputChanged += OnInputChanged;
            chatInputBox.InputSubmitted += OnInputSubmitted;

            closePopupTask = new UniTaskCompletionSource();

            CurrentChannel = defaultChannelId;
        }

        public ChatChannel.ChannelId CurrentChannel
        {
            get => currentChannel.Id;

            set
            {
                if (currentChannel == null || !currentChannel.Id.Equals(value))
                {
                    currentChannel = channels[value];

                    chatMessageViewer.SetData(currentChannel.Messages);
                }
            }
        }

        /// <summary>
        /// Opens or closes the chat window.
        /// </summary>
        /// <param name="show">Whether to open or close it.</param>
        public void ToggleChat(bool show)
        {
            panelBackgroundCanvasGroup.gameObject.SetActive(show);
            chatMessageViewer.SetVisibility(show);
        }

        /// <summary>
        /// Makes the input box stop receiving user inputs.
        /// </summary>
        public void DisableInputBoxSubmissions()
        {
            chatInputBox.DisableInputBoxSubmissions();
        }

        /// <summary>
        /// Makes the input box start receiving user inputs.
        /// </summary>
        public void EnableInputBoxSubmissions()
        {
            chatInputBox.EnableInputBoxSubmissions();
        }

        /// <summary>
        /// Makes the input box gain the focus. It does not modify its content.
        /// </summary>
        public void FocusInputBox()
        {
            chatInputBox.FocusInputBox();
        }

        /// <summary>
        /// Makes the input box gain the focus and replaces its content.
        /// </summary>
        /// <param name="text">The new content of the input box.</param>
        public void FocusInputBoxWithText(string text)
        {
            if (gameObject.activeInHierarchy)
            {
                chatInputBox.FocusInputBoxWithText(text);
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
        /// Makes sure the chat window is showing all the messages stored in the data for the current channel.
        /// </summary>
        public void RefreshMessages()
        {
            chatMessageViewer.RefreshMessages();
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
        public void PlayMessageReceivedSfx()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(chatReceiveMessageAudio);
        }

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
            chatMessageViewer.SetScrollbarVisibility(false, BackgroundFadeTime);
            chatMessageViewer.StartChatEntriesFadeout();
        }

        public override UniTask HideAsync(CancellationToken ct, bool isInstant = false)
        {
            closePopupTask.TrySetResult();
            chatInputBox.OnViewHide();
            return base.HideAsync(ct, isInstant);
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
            chatInputBox.InjectDependencies(dependencies);
            chatMessageViewer.InjectDependencies(dependencies);
        }

        private void OnInputBoxSelectionChanged(bool isSelected)
        {
            if (isSelected)
            {
                if (isChatClosed)
                {
                    isChatClosed = false;
                    ToggleChat(true);
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

            var data = new ChatEntryMenuPopupData(
                chatEntryView.messageBubbleElement.popupPosition.position,
                messageText,
                closePopupTask.Task);

            viewDependencies.GlobalUIViews.ShowChatEntryMenuPopupAsync(data);
        }

        private void CloseChat()
        {
            isChatClosed = true;
            ToggleChat(false);
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
            InputSubmitted?.Invoke(currentChannel, messageToSend, origin);
        }

        private void OnToggleChatBubblesValueChanged(bool isToggled)
        {
            ChatBubbleVisibilityChanged?.Invoke(isToggled);
        }

        private Color CalculateUsernameColor(ChatMessage chatMessage) =>
            chatEntryConfiguration.GetNameColor(chatMessage.SenderValidatedName);

    }
}
