using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Emoji;
using DCL.UI;
using MVC;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
    // Note: The view never changes any data (chatMessages), that's done by the controller
    public class ChatView : ViewBase, IView, IViewWithGlobalDependencies, IPointerEnterHandler, IPointerExitHandler, IDisposable
    {
        public delegate void InputBoxFocusChangedDelegate(bool hasFocus);
        public delegate void EmojiSelectionVisibilityChangedDelegate(bool isVisible);
        public delegate void InputSubmittedDelegate(ChatChannel channel, string message, string origin);
        public delegate void ScrollBottomReachedDelegate();
        public delegate void PointerEventDelegate();
        public delegate void ChatBubbleVisibilityChangedDelegate(bool isVisible);

        [Header("Settings")]
        [Tooltip("The time it takes, in seconds, for the background of the chat window to fade-in/out when hovering with the mouse.")]
        [SerializeField]
        private float BackgroundFadeTime = 0.2f;

        [Tooltip("The maximum amount of character allowed in the input box.")]
        [SerializeField]
        private int MaxMessageLength = 250;

        [Header("UI elements")]
        [FormerlySerializedAs("ChatBubblesToggle")]
        [SerializeField]
        private ToggleView chatBubblesToggle;

        [FormerlySerializedAs("InputField")]
        [SerializeField]
        private TMP_InputField inputField;

        [FormerlySerializedAs("CharacterCounter")]
        [SerializeField]
        private CharacterCounterView characterCounter;

        [FormerlySerializedAs("PanelBackgroundCanvasGroup")]
        [SerializeField]
        private CanvasGroup panelBackgroundCanvasGroup;

        [FormerlySerializedAs("CloseChatButton")]
        [SerializeField]
        private Button closeChatButton;

        [SerializeField]
        private RectTransform pastePopupPosition;

        [SerializeField]
        private ChatMessageViewerElement chatMessageViewer;

        [SerializeField]
        private Button scrollToBottomButton;

        [SerializeField]
        private TMP_Text scrollToBottomNumberText;

        [Header("Emojis")]

        [FormerlySerializedAs("EmojiPanel")]
        [SerializeField]
        private EmojiPanelView emojiPanel;

        [FormerlySerializedAs("EmojiSuggestionPanel")]
        [SerializeField]
        private EmojiSuggestionPanelView emojiSuggestionPanel;

        [FormerlySerializedAs("EmojiPanelButton")]
        [SerializeField]
        private EmojiButtonView emojiPanelButton;

        [SerializeField]
        private EmojiPanelConfigurationSO emojiPanelConfiguration;

        [SerializeField]
        private TextAsset emojiMappingJson;

        [SerializeField]
        private EmojiSectionView emojiSectionViewPrefab;

        [SerializeField]
        private EmojiButton emojiButtonPrefab;

        [SerializeField]
        private EmojiSuggestionView emojiSuggestionViewPrefab;

        [Header("Audio")]
        [SerializeField]
        private AudioClipConfig addEmojiAudio;

        [SerializeField]
        private AudioClipConfig openEmojiPanelAudio;

        [SerializeField]
        private AudioClipConfig chatSendMessageAudio;

        [SerializeField]
        private AudioClipConfig chatReceiveMessageAudio;

        [SerializeField]
        private AudioClipConfig chatInputTextAudio;

        [SerializeField]
        private AudioClipConfig enterInputAudio;

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

        private const string EMOJI_SUGGESTION_PATTERN = @":\w+";
        private const string EMOJI_TAG = "[emoji]";
        private const string ORIGIN = "chat";
        private static readonly Regex EMOJI_PATTERN_REGEX = new (EMOJI_SUGGESTION_PATTERN, RegexOptions.Compiled);

        private ViewDependencies viewDependencies;

        private EmojiPanelController? emojiPanelController;
        private EmojiSuggestionPanel? emojiSuggestionPanelController;
        private readonly List<EmojiData> keysWithPrefix = new ();

        private CancellationTokenSource emojiSearchCts;
        private CancellationTokenSource emojiPanelCts;
        private UniTaskCompletionSource closePopupTask;

        private Mouse device;

        private bool isInputSelected;
        private IReadOnlyDictionary<ChatChannel.ChannelId, ChatChannel>? channels;
        private ChatChannel? currentChannel;
        private ChatEntryConfigurationSO chatEntryConfiguration;

        /// <summary>
        /// Get or sets the current content of the input box.
        /// </summary>
        public string InputBoxText
        {
            get => inputField.text;
            set => inputField.text = value;
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
            get
            {
                return chatBubblesToggle.Toggle.interactable;
            }

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
        /// Gets or sets whether the Unread messages count (AKA scroll-to-bottom button) is visible or not.
        /// </summary>
        public bool IsUnreadMessagesCountVisible
        {
            get => scrollToBottomButton.gameObject.activeInHierarchy;
            set => scrollToBottomButton.gameObject.SetActive(value);
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
        }

        public void Initialize(IReadOnlyDictionary<ChatChannel.ChannelId, ChatChannel> chatChannels, ChatChannel.ChannelId defaultChannelId, bool areChatBubblesVisible, ChatEntryConfigurationSO chatEntryConfiguration)
        {
            device = InputSystem.GetDevice<Mouse>();
            this.channels = chatChannels;
            this.chatEntryConfiguration = chatEntryConfiguration;

            characterCounter.SetMaximumLength(inputField.characterLimit);
            characterCounter.gameObject.SetActive(false);

            inputField.onValueChanged.AddListener(OnInputChanged);
            inputField.onSelect.AddListener(OnInputSelected);
            inputField.onDeselect.AddListener(OnInputDeselected);
            closeChatButton.onClick.AddListener(CloseChat);
            chatMessageViewer.Initialize(CalculateUsernameColor);
            chatMessageViewer.ChatMessageOptionsButtonClicked += OnChatMessageOptionsButtonClicked;
            chatMessageViewer.ChatMessageViewerScrollPositionChanged += OnChatMessageViewerScrollPositionChanged;
            scrollToBottomButton.onClick.AddListener(OnScrollToEndButtonClicked);

            chatBubblesToggle.IsSoundEnabled = false;
            chatBubblesToggle.Toggle.isOn = areChatBubblesVisible;
            chatBubblesToggle.Toggle.onValueChanged.AddListener(OnToggleChatBubblesValueChanged);
            chatBubblesToggle.IsSoundEnabled = true;

            InitializeEmojiController();

            viewDependencies.DclInput.UI.RightClick.performed += OnRightClickRegistered;
            closePopupTask = new UniTaskCompletionSource();

            CurrentChannel = defaultChannelId;
        }

        private void OnScrollToEndButtonClicked()
        {
            chatMessageViewer.ShowLastMessage(true);
        }

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
        /// Gets or sets whether the chat panel is open or close (the input box is visible in any case).
        /// </summary>
        public bool IsUnfolded
        {
            get => panelBackgroundCanvasGroup.gameObject.activeInHierarchy;

            set
            {
                if(value == panelBackgroundCanvasGroup.gameObject.activeInHierarchy)
                    return;

                panelBackgroundCanvasGroup.gameObject.SetActive(value);
                chatMessageViewer.SetVisibility(value);

                if (!value)
                {
                    chatMessageViewer.HideSeparator();
                    IsUnreadMessagesCountVisible = false;
                }
                else
                {
                    chatMessageViewer.ShowLastMessage();
                }
            }
        }

        /// <summary>
        /// Makes the input box stop receiving user inputs.
        /// </summary>
        public void DisableInputBoxSubmissions()
        {
            viewDependencies.ClipboardManager.OnPaste -= PasteClipboardText;
            inputField.onSubmit.RemoveAllListeners();
            inputField.DeactivateInputField();
        }

        /// <summary>
        /// Makes the input box start receiving user inputs.
        /// </summary>
        public void EnableInputBoxSubmissions()
        {
            inputField.onSubmit.AddListener(OnSubmit);
        }

        /// <summary>
        /// Makes the input box gain the focus. It does not modify its content.
        /// </summary>
        public void FocusInputBox()
        {
            if (emojiSuggestionPanelController is { IsActive: true }) return;
            if (inputField.isFocused) return;

            inputField.OnSelect(null);
        }

        /// <summary>
        /// Makes the input box gain the focus and replaces its content.
        /// </summary>
        /// <param name="text">The new content of the input box.</param>
        public void FocusInputBoxWithText(string text)
        {
            if (gameObject.activeInHierarchy && inputField.isFocused == false)
            {
                inputField.text = text;
                inputField.ActivateInputField();
                inputField.caretPosition = inputField.text.Length;
            }
        }

        /// <summary>
        /// Makes the chat submit the current content of the input box.
        /// </summary>
        public void SubmitInput()
        {
            inputField.OnSubmit(new BaseEventData(null));
        }

        /// <summary>
        /// Makes sure the chat window is showing all the messages stored in the data for the current channel.
        /// </summary>
        public void RefreshMessages()
        {
            int pendingMessages = currentChannel!.Messages.Count - currentChannel.ReadMessages;

            if(pendingMessages > 0 && !chatMessageViewer.IsSeparatorVisible)
                chatMessageViewer.ShowSeparator(pendingMessages + 1);

            chatMessageViewer.RefreshMessages();

            IsUnreadMessagesCountVisible = pendingMessages != 0;

            if (pendingMessages > 0)
                scrollToBottomNumberText.text = pendingMessages > 9 ? "+9" : pendingMessages.ToString();
        }

        /// <summary>
        /// Performs a click event on the chat window.
        /// </summary>
        public void Click()
        {
            CheckIfClickedOnEmojiPanel();

            void CheckIfClickedOnEmojiPanel()
            {
                if (!(emojiPanel.gameObject.activeInHierarchy ||
                      emojiSuggestionPanel.gameObject.activeInHierarchy)) return;

                IReadOnlyList<RaycastResult> raycastResults = viewDependencies.EventSystem.RaycastAll(device.position.value);
                bool clickedOnPanel = false;

                foreach (RaycastResult result in raycastResults)
                    if (result.gameObject == emojiPanel.gameObject ||
                        result.gameObject == emojiSuggestionPanel.ScrollView.gameObject ||
                        result.gameObject == emojiPanelButton.gameObject)
                        clickedOnPanel = true;

                if (!clickedOnPanel)
                {
                    if (emojiPanel.gameObject.activeInHierarchy)
                    {
                        emojiPanelButton.SetState(false);
                        emojiPanel.gameObject.SetActive(false);
                        EmojiSelectionVisibilityChanged?.Invoke(false);
                    }

                    emojiSuggestionPanelController!.SetPanelVisibility(false);
                }
            }
        }

        /// <summary>
        /// Plays the sound FX of the chat receiving a new message.
        /// </summary>
        public void PlayMessageReceivedSfx()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(chatReceiveMessageAudio);
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
            chatMessageViewer.SetScrollbarVisibility(false, BackgroundFadeTime);
            chatMessageViewer.StartChatEntriesFadeout();
        }

        public void Dispose()
        {
            if (emojiPanelController != null)
            {
                emojiPanelController.EmojiSelected -= AddEmojiToInput;
                emojiPanelController.Dispose();
            }

            if (emojiSuggestionPanelController != null)
                emojiSuggestionPanelController.EmojiSelected -= AddEmojiFromSuggestion;

            emojiPanelCts.SafeCancelAndDispose();
            emojiSearchCts.SafeCancelAndDispose();

            viewDependencies.DclInput.UI.RightClick.performed -= OnRightClickRegistered;
        }

        private void Start()
        {
            panelBackgroundCanvasGroup.alpha = 0;
        }

        private void CloseChat()
        {
            IsUnfolded = false;
        }

        private void ToggleEmojiPanel()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(openEmojiPanelAudio);

            emojiPanelCts = emojiPanelCts.SafeRestart();
            bool toggle = !emojiPanel.gameObject.activeInHierarchy;
            emojiPanel.gameObject.SetActive(toggle);
            emojiPanelButton.SetState(toggle);
            emojiSuggestionPanelController!.SetPanelVisibility(false);
            emojiPanel.EmojiContainer.gameObject.SetActive(toggle);
            inputField.ActivateInputField();

            EmojiSelectionVisibilityChanged?.Invoke(toggle);
        }

        private void PasteClipboardText(object sender, string pastedText)
        {
            if (!inputField.isActiveAndEnabled) return;

            int remainingSpace = inputField.characterLimit - inputField.text.Length;

            if (remainingSpace <= 0) return;

            int caretPosition = inputField.stringPosition;
            string textToInsert = pastedText.Length > remainingSpace ? pastedText[..remainingSpace] : pastedText;

            inputField.text = inputField.text.Insert(caretPosition, textToInsert);
            inputField.stringPosition += textToInsert.Length;
            inputField.ActivateInputField();
            characterCounter.SetCharacterCount(inputField.text.Length);
            chatMessageViewer.StopChatEntriesFadeout();
        }

        public override UniTask HideAsync(CancellationToken ct, bool isInstant = false)
        {
            closePopupTask.TrySetResult();
            return base.HideAsync(ct, isInstant);
        }

        private void OnInputDeselected(string inputText)
        {
            isInputSelected = false;
            emojiPanelButton.SetColor(false);
            characterCounter.gameObject.SetActive(false);
            chatMessageViewer.StartChatEntriesFadeout();
            InputBoxFocusChanged?.Invoke(false);
        }

        private void OnInputSelected(string inputText)
        {
            if (!IsUnfolded)
            {
                IsUnfolded = true;
                chatMessageViewer.ShowLastMessage();
            }

            UIAudioEventsBus.Instance.SendPlayAudioEvent(enterInputAudio);

            if (isInputSelected) return;

            isInputSelected = true;
            emojiPanelButton.SetColor(true);
            characterCounter.gameObject.SetActive(true);
            chatMessageViewer.StopChatEntriesFadeout();
            InputBoxFocusChanged?.Invoke(true);
        }

        private void OnInputChanged(string inputText)
        {
            HandleEmojiSearch(inputText);
            UIAudioEventsBus.Instance.SendPlayAudioEvent(chatInputTextAudio);
            closePopupTask.TrySetResult();
            characterCounter.SetCharacterCount(inputText.Length);
            chatMessageViewer.StopChatEntriesFadeout();
        }

        private void OnSubmit(string _)
        {
            if (emojiSuggestionPanelController is { IsActive: true })
            {
                emojiSuggestionPanelController!.SetPanelVisibility(false);
                return;
            }

            if (emojiPanel.gameObject.activeInHierarchy)
            {
                emojiPanelButton.SetState(false);
                emojiPanelController!.SetPanelVisibility(false);
                EmojiSelectionVisibilityChanged?.Invoke(false);
            }

            if (string.IsNullOrWhiteSpace(inputField.text))
            {
                inputField.DeactivateInputField();
                inputField.OnDeselect(null);
                return;
            }

            UIAudioEventsBus.Instance.SendPlayAudioEvent(chatSendMessageAudio);
            string messageToSend = inputField.text;

            inputField.text = string.Empty;
            inputField.ActivateInputField();

            InputSubmitted?.Invoke(currentChannel!, messageToSend, ORIGIN);
        }

        private void OnToggleChatBubblesValueChanged(bool isToggled)
        {
            ChatBubbleVisibilityChanged?.Invoke(isToggled);
        }

        private void OnRightClickRegistered(InputAction.CallbackContext _)
        {
            if (isInputSelected && viewDependencies.ClipboardManager.HasValue())
            {
                viewDependencies.ClipboardManager.OnPaste -= PasteClipboardText;
                viewDependencies.ClipboardManager.OnPaste += PasteClipboardText;
                closePopupTask.TrySetResult();
                closePopupTask = new UniTaskCompletionSource();

                var data = new PastePopupToastData(
                    pastePopupPosition.position,
                    closePopupTask.Task);

                viewDependencies.GlobalUIViews.ShowPastePopupToastAsync(data);
                inputField.ActivateInputField();
            }
        }

        private void OnChatMessageOptionsButtonClicked(string messageText, ChatEntryView chatEntryView)
        {
            closePopupTask.TrySetResult();
            closePopupTask = new UniTaskCompletionSource();

            ChatEntryMenuPopupData data = new ChatEntryMenuPopupData(
                chatEntryView.messageBubbleElement.popupPosition.position,
                messageText,
                closePopupTask.Task);

            viewDependencies.GlobalUIViews.ShowChatEntryMenuPopupAsync(data);
        }

        private bool IsWithinCharacterLimit() =>
            inputField.text.Length < inputField.characterLimit;

        private Color CalculateUsernameColor(ChatMessage chatMessage) =>
            chatEntryConfiguration.GetNameColor(chatMessage.SenderValidatedName);

        private void OnChatMessageViewerScrollPositionChanged(Vector2 scrollPosition)
        {
            if (chatMessageViewer.IsScrollAtBottom)
            {
                IsUnreadMessagesCountVisible = false;

                ScrollBottomReached?.Invoke();
            }
        }

        #region Emojis

        private void InitializeEmojiController()
        {
            emojiPanelController = new EmojiPanelController(emojiPanel, emojiPanelConfiguration, emojiMappingJson, emojiSectionViewPrefab, emojiButtonPrefab);
            emojiPanelController.EmojiSelected += AddEmojiToInput;

            emojiSuggestionPanelController = new EmojiSuggestionPanel(emojiSuggestionPanel, emojiSuggestionViewPrefab);
            emojiSuggestionPanelController.InjectDependencies(viewDependencies);
            emojiSuggestionPanelController.EmojiSelected += AddEmojiFromSuggestion;

            emojiPanelButton.Button.onClick.AddListener(ToggleEmojiPanel);
        }

        private void AddEmojiFromSuggestion(string emojiCode, bool shouldClose)
        {
            if (!IsWithinCharacterLimit()) return;

            UIAudioEventsBus.Instance.SendPlayAudioEvent(addEmojiAudio);
            inputField.SetTextWithoutNotify(inputField.text.Replace(EMOJI_PATTERN_REGEX.Match(inputField.text).Value, emojiCode));
            inputField.stringPosition += emojiCode.Length;
            inputField.ActivateInputField();

            if (shouldClose)
                emojiSuggestionPanelController!.SetPanelVisibility(false);
        }

        private void AddEmojiToInput(string emoji)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(addEmojiAudio);

            if (!IsWithinCharacterLimit()) return;

            int caretPosition = inputField.stringPosition;
            inputField.text = inputField.text.Insert(caretPosition, EMOJI_TAG);
            inputField.text = inputField.text.Replace(EMOJI_TAG, emoji);
            inputField.stringPosition += emoji.Length;

            inputField.ActivateInputField();
        }

        private void HandleEmojiSearch(string inputText)
        {
            Match match = EMOJI_PATTERN_REGEX.Match(inputText);

            if (match.Success)
            {
                if (match.Value.Length < 2)
                {
                    emojiSuggestionPanelController!.SetPanelVisibility(false);
                    return;
                }

                emojiSearchCts.SafeCancelAndDispose();
                emojiSearchCts = new CancellationTokenSource();

                SearchAndSetEmojiSuggestionsAsync(match.Value, emojiSearchCts.Token).Forget();
            }
            else
            {
                if (emojiSuggestionPanelController is { IsActive: true })
                    emojiSuggestionPanelController!.SetPanelVisibility(false);
            }
        }

        private async UniTaskVoid SearchAndSetEmojiSuggestionsAsync(string value, CancellationToken ct)
        {
            await DictionaryUtils.GetKeysWithPrefixAsync(emojiPanelController!.EmojiNameMapping, value, keysWithPrefix, ct);

            emojiSuggestionPanelController!.SetValues(keysWithPrefix);
            emojiSuggestionPanelController!.SetPanelVisibility(true);
        }

        #endregion
    }
}
