using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Clipboard;
using DCL.Emoji;
using DCL.Input;
using DCL.UI;
using MVC;
using DG.Tweening;
using SuperScrollView;
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
    public class ChatView : ViewBase, IViewWithGlobalDependencies, IPointerEnterHandler, IPointerExitHandler, IDisposable
    {
        /// <summary>
        /// The prefab to use when instantiating a new item.
        /// </summary>
        private enum ChatItemPrefabIndex // It must match the list in the LoopListView.
        {
            ChatEntry,
            ChatEntryOwn,
            Padding,
            SystemChatEntry
        }

        public delegate Color CalculateUsernameColorDelegate(ChatMessage chatMessage);
        public delegate void InputBoxFocusChangedDelegate(bool hasFocus);
        public delegate void EmojiSelectionVisibilityChangedDelegate(bool isVisible);
        public delegate void InputSubmittedDelegate(string message, string origin);
        public delegate void ChatMessageCreatedDelegate(int itemIndex);

        [Header("Settings")]
        [Tooltip("The time it takes, in seconds, for the background of the chat window to fade-in/out when hovering with the mouse.")]
        [SerializeField]
        private float BackgroundFadeTime = 0.2f;

        [Tooltip("The time it takes, in seconds, for a new chat entry to fade-in.")]
        [SerializeField]
        private float ChatEntriesFadeTime = 3f;

        [Tooltip("The time it takes, in milliseconds, without focus before the entire chat window starts fading out.")]
        [SerializeField]
        private int ChatEntriesWaitBeforeFading = 10000;

        [Tooltip("The maximum amount of character allowed in the input box.")]
        [SerializeField]
        private int MaxMessageLength = 250;

        [Header("UI elements")]
        [FormerlySerializedAs("ChatBubblesToggle")]
        [SerializeField]
        private ToggleView chatBubblesToggle;

        [FormerlySerializedAs("EmojiPanel")]
        [SerializeField]
        private EmojiPanelView emojiPanel;

        [FormerlySerializedAs("EmojiSuggestionPanel")]
        [SerializeField]
        private EmojiSuggestionPanelView emojiSuggestionPanel;

        [FormerlySerializedAs("InputField")]
        [SerializeField]
        private TMP_InputField inputField;

        [FormerlySerializedAs("CharacterCounter")]
        [SerializeField]
        private CharacterCounterView characterCounter;

        [FormerlySerializedAs("PanelBackgroundCanvasGroup")]
        [SerializeField]
        private CanvasGroup panelBackgroundCanvasGroup;

        [FormerlySerializedAs("ScrollbarCanvasGroup")]
        [SerializeField]
        private CanvasGroup scrollbarCanvasGroup;

        [FormerlySerializedAs("ChatEntriesCanvasGroup")]
        [SerializeField]
        private CanvasGroup chatEntriesCanvasGroup;

        [FormerlySerializedAs("LoopList")]
        [SerializeField]
        private LoopListView2 loopList;

        [FormerlySerializedAs("EmojiPanelButton")]
        [SerializeField]
        private EmojiButtonView emojiPanelButton;

        [FormerlySerializedAs("CloseChatButton")]
        [SerializeField]
        private Button closeChatButton;

        [Header("Dependencies")]
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

        [SerializeField]
        private RectTransform pastePopupPosition;

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
        /// Raised when a new UI element that displays a chat message is created.
        /// </summary>
        public event ChatMessageCreatedDelegate ChatMessageCreated;

        /// <summary>
        /// Raised when the option to change the visibility of the chat bubbles over the avatar changes its value.
        /// </summary>
        public event Action<bool>? ChatBubbleVisibilityChanged;

        /// <summary>
        /// An external function that provides a way to calculate the color to be used to display a user name.
        /// </summary>
        public CalculateUsernameColorDelegate CalculateUsernameColor;

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
        private CancellationTokenSource fadeoutCts;
        private UniTaskCompletionSource closePopupTask;

        private Mouse device;

        private bool isChatClosed;
        private bool isInputSelected;
        private IReadOnlyList<ChatMessage> chatMessages;

        /// <summary>
        /// Gets whether the scroll view is showing the bottom of the content, and it can't scroll down anymore.
        /// </summary>
        public bool IsScrollAtBottom => loopList.ScrollRect.normalizedPosition.y <= 0.0f;

        /// <summary>
        /// Gets whether the scroll view is showing the top of the content, and it can't scroll up anymore.
        /// </summary>
        public bool IsScrollAtTop => loopList.ScrollRect.normalizedPosition.y >= 1.0f;

        /// <summary>
        /// Get or sets the current content of the input box.
        /// </summary>
        public string InputBoxText
        {
            get => inputField.text;
            set => inputField.text = value;
        }

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

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
        }

        public void Initialize(IReadOnlyList<ChatMessage> chatMessages, bool areChatBubblesVisible)
        {
            device = InputSystem.GetDevice<Mouse>();

            characterCounter.SetMaximumLength(inputField.characterLimit);
            characterCounter.gameObject.SetActive(false);

            inputField.onValueChanged.AddListener(OnInputChanged);
            inputField.onSelect.AddListener(OnInputSelected);
            inputField.onDeselect.AddListener(OnInputDeselected);
            closeChatButton.onClick.AddListener(CloseChat);
            loopList.InitListView(0, OnGetItemByIndex);

            chatBubblesToggle.IsSoundEnabled = false;
            chatBubblesToggle.Toggle.isOn = areChatBubblesVisible;
            chatBubblesToggle.Toggle.onValueChanged.AddListener(OnToggleChatBubblesValueChanged);
            chatBubblesToggle.IsSoundEnabled = true;

            InitializeEmojiController();

            viewDependencies.DclInput.UI.RightClick.performed += OnRightClickRegistered;
            closePopupTask = new UniTaskCompletionSource();

            SetMessages(chatMessages);
        }

        /// <summary>
        /// Replaces the message data of the view with another.
        /// </summary>
        /// <param name="messages">The new messages to display in the view.</param>
        public void SetMessages(IReadOnlyList<ChatMessage> messages)
        {
            // TODO: In the near future, this will produce a complete rebuild of the message entries
            chatMessages = messages;
        }

        /// <summary>
        /// Opens or closes the chat window.
        /// </summary>
        /// <param name="show">Whether to open or close it.</param>
        public void ToggleChat(bool show)
        {
            panelBackgroundCanvasGroup.gameObject.SetActive(show);
            loopList.gameObject.SetActive(show);
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
        /// Moves the chat so it shows the last created message.
        /// </summary>
        public void ShowLastMessage()
        {
            loopList.MovePanelToItemIndex(0, 0);
        }

        /// <summary>
        /// Makes sure the chat window is showing all the messages stored in the data.
        /// </summary>
        public void RefreshMessages()
        {
            ResetChatEntriesFadeout();
            loopList.SetListItemCount(chatMessages.Count);
            ShowLastMessage();

// DISABLED UNTIL UNREAD MESSAGES FEATURE IS MERGED

            // Scroll view adjustment
//            if (IsScrollAtBottom)
//            {
//                loopList.MovePanelToItemIndex(0, 0);
//            }
//            else
//            {
//                loopList.RefreshAllShownItem();

                // When the scroll view is not at the bottom, chat messages should not move if a new message is added
                // An offset has to be applied to the scroll view in order to prevent messages from moving
//                LoopListViewItem2 addedItem = loopList.GetShownItemByIndex(1);
//                float offsetToPreventScrollViewMovement = -addedItem.ItemSize - addedItem.Padding;
//                loopList.MovePanelByOffset(offsetToPreventScrollViewMovement);

                // Known issue: When the scroll view is at the top, the scroll view moves a bit downwards
//            }
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

                    emojiSuggestionPanelController!.HideAsync(CancellationToken.None);
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

        public void OnPointerEnter(PointerEventData eventData)
        {
            PointerEnter?.Invoke();
            panelBackgroundCanvasGroup.DOFade(1, BackgroundFadeTime);
            scrollbarCanvasGroup.DOFade(1, BackgroundFadeTime);
            StopChatEntriesFadeout();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            PointerExit?.Invoke();
            panelBackgroundCanvasGroup.DOFade(0, BackgroundFadeTime);
            scrollbarCanvasGroup.DOFade(0, BackgroundFadeTime);
            StartChatEntriesFadeout();
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

            fadeoutCts.SafeCancelAndDispose();
            emojiPanelCts.SafeCancelAndDispose();
            emojiSearchCts.SafeCancelAndDispose();

            viewDependencies.DclInput.UI.RightClick.performed -= OnRightClickRegistered;
        }

        private void Start()
        {
            panelBackgroundCanvasGroup.alpha = 0;
            scrollbarCanvasGroup.alpha = 0;
        }

        // Called by the LoopListView when the number of items change, it uses out data (chatMessages)
        // to customize a new instance of the ChatEntryView (it uses pools internally).
        private LoopListViewItem2? OnGetItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= chatMessages.Count)
                return null;

            ChatMessage itemData = chatMessages[index];
            LoopListViewItem2 item;

            if (itemData.IsPaddingElement)
                item = listView.NewListViewItem(listView.ItemPrefabDataList[(int)ChatItemPrefabIndex.Padding].mItemPrefab.name);
            else
            {
                item = listView.NewListViewItem(itemData.SystemMessage ? listView.ItemPrefabDataList[(int)ChatItemPrefabIndex.SystemChatEntry].mItemPrefab.name :
                    itemData.SentByOwnUser ? listView.ItemPrefabDataList[(int)ChatItemPrefabIndex.ChatEntryOwn].mItemPrefab.name
                                            : listView.ItemPrefabDataList[(int)ChatItemPrefabIndex.ChatEntry].mItemPrefab.name);

                ChatEntryView itemScript = item!.GetComponent<ChatEntryView>()!;
                SetItemData(index, itemData, itemScript);

                Button? messageOptionsButton = itemScript.messageBubbleElement.messageOptionsButton;
                messageOptionsButton?.onClick.RemoveAllListeners();

                messageOptionsButton?.onClick.AddListener(() =>
                    OnChatMessageOptionsButtonClicked(itemData.Message, itemScript));
            }

            ChatMessageCreated?.Invoke(index);

            return item;
        }

        private void SetItemData(int index, ChatMessage itemData, ChatEntryView itemView)
        {
            //temporary approach to extract the username without the walledId, will be refactored
            //once we have the proper integration of the profile retrieval
            Color playerNameColor = CalculateUsernameColor(itemData);

            itemView.usernameElement.userName.color = playerNameColor;

            if (!itemData.SystemMessage)
            {
                itemView.ProfileBackground!.color = playerNameColor;
                playerNameColor.r += 0.3f;
                playerNameColor.g += 0.3f;
                playerNameColor.b += 0.3f;
                itemView.ProfileOutline!.color = playerNameColor;
            }

            itemView.SetItemData(itemData);

            //Workaround needed to animate the chat entries due to infinite scroll plugin behaviour
            if (itemData.HasToAnimate)
            {
                itemView.AnimateChatEntry();
                // Note: itemData.HasToAnimate is set to false by the controller later
            }
        }

        private void StopChatEntriesFadeout()
        {
            fadeoutCts.SafeCancelAndDispose();
            chatEntriesCanvasGroup.alpha = 1;
        }

        private void StartChatEntriesFadeout()
        {
            fadeoutCts.SafeCancelAndDispose();
            fadeoutCts = new CancellationTokenSource();

            AwaitAndFadeChatEntriesAsync(fadeoutCts.Token).Forget();
        }

        private void ResetChatEntriesFadeout()
        {
            StopChatEntriesFadeout();
            StartChatEntriesFadeout();
        }

        private async UniTaskVoid AwaitAndFadeChatEntriesAsync(CancellationToken ct)
        {
            fadeoutCts.Token.ThrowIfCancellationRequested();
            chatEntriesCanvasGroup.alpha = 1;
            await UniTask.Delay(ChatEntriesWaitBeforeFading, cancellationToken: ct);
            await chatEntriesCanvasGroup.DOFade(0.4f, ChatEntriesFadeTime).ToUniTask(cancellationToken: ct);
        }

        private void CloseChat()
        {
            isChatClosed = true;
            ToggleChat(false);
        }

        private void ToggleEmojiPanel()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(openEmojiPanelAudio);

            emojiPanelCts = emojiPanelCts.SafeRestart();
            bool toggle = !emojiPanel.gameObject.activeInHierarchy;
            emojiPanel.gameObject.SetActive(toggle);
            emojiPanelButton.SetState(toggle);
            emojiSuggestionPanelController!.HideAsync(CancellationToken.None);
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
            StopChatEntriesFadeout();
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
            StartChatEntriesFadeout();
            InputBoxFocusChanged?.Invoke(false);
        }

        private void OnInputSelected(string inputText)
        {
            if (isChatClosed)
            {
                isChatClosed = false;
                ToggleChat(true);
                ShowLastMessage();
            }

            UIAudioEventsBus.Instance.SendPlayAudioEvent(enterInputAudio);

            if (isInputSelected) return;

            isInputSelected = true;
            emojiPanelButton.SetColor(true);
            characterCounter.gameObject.SetActive(true);
            StopChatEntriesFadeout();
            InputBoxFocusChanged?.Invoke(true);
        }

        private void OnInputChanged(string inputText)
        {
            HandleEmojiSearch(inputText);
            UIAudioEventsBus.Instance.SendPlayAudioEvent(chatInputTextAudio);
            closePopupTask.TrySetResult();
            characterCounter.SetCharacterCount(inputText.Length);
            StopChatEntriesFadeout();
        }

        private void OnSubmit(string _)
        {
            if (emojiSuggestionPanelController is { IsActive: true })
            {
                emojiSuggestionPanelController.HideAsync(CancellationToken.None);
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

            InputSubmitted?.Invoke(messageToSend, ORIGIN);
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
                emojiSuggestionPanelController!.HideAsync(CancellationToken.None);
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
                    emojiSuggestionPanelController!.HideAsync(CancellationToken.None);
                    return;
                }

                emojiSearchCts.SafeCancelAndDispose();
                emojiSearchCts = new CancellationTokenSource();

                SearchAndSetEmojiSuggestionsAsync(match.Value, emojiSearchCts.Token).Forget();
            }
            else
            {
                if (emojiSuggestionPanelController is { IsActive: true })
                    emojiSuggestionPanelController!.HideAsync(CancellationToken.None);
            }
        }

        private async UniTaskVoid SearchAndSetEmojiSuggestionsAsync(string value, CancellationToken ct)
        {
            await DictionaryUtils.GetKeysWithPrefixAsync(emojiPanelController!.EmojiNameMapping, value, keysWithPrefix, ct);

            emojiSuggestionPanelController!.SetValues(keysWithPrefix);
            emojiSuggestionPanelController.ShowAsync(CancellationToken.None);
        }

        #endregion
    }
}
