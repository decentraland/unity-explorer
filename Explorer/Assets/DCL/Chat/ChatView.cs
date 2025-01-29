using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.UI;
using MVC;
using DG.Tweening;
using SuperScrollView;
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

        [SerializeField]
        private ChatInputBoxElement chatInputBox;

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

        [FormerlySerializedAs("CloseChatButton")]
        [SerializeField]
        private Button closeChatButton;

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

        /// <summary>
        ///     An external function that provides a way to calculate the color to be used to display a user name.
        /// </summary>
        public CalculateUsernameColorDelegate CalculateUsernameColor;
        private IReadOnlyList<ChatMessage> chatMessages;
        private UniTaskCompletionSource closePopupTask;

        // The latest amount of messages added to the chat that must be animated yet
        private int entriesPendingToAnimate;
        private CancellationTokenSource fadeoutCts;

        private bool isChatClosed;
        private bool isInputSelected;

        private ViewDependencies viewDependencies;

        /// <summary>
        ///     Gets whether the scroll view is showing the bottom of the content, and it can't scroll down anymore.
        /// </summary>
        public bool IsScrollAtBottom => loopList.ScrollRect.normalizedPosition.y <= 0.0f;

        /// <summary>
        ///     Gets whether the scroll view is showing the top of the content, and it can't scroll up anymore.
        /// </summary>
        public bool IsScrollAtTop => loopList.ScrollRect.normalizedPosition.y >= 1.0f;

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
            scrollbarCanvasGroup.alpha = 0;
        }

        public void Dispose()
        {
            chatInputBox.Dispose();
            fadeoutCts.SafeCancelAndDispose();
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

        public override UniTask HideAsync(CancellationToken ct, bool isInstant = false)
        {
            chatInputBox.OnViewHide();
            return base.HideAsync(ct, isInstant);
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
            chatInputBox.InjectDependencies(dependencies);
        }

        /// <summary>
        ///     Raised when the mouse pointer hovers any part of the chat window.
        /// </summary>
        public event Action PointerEnter;

        /// <summary>
        ///     Raised when the mouse pointer stops hovering the chat window.
        /// </summary>
        public event Action PointerExit;

        /// <summary>
        ///     Raised when either the input box gains the focus or loses it.
        /// </summary>
        public event InputBoxFocusChangedDelegate InputBoxFocusChanged;

        /// <summary>
        ///     Raised when either the emoji selection panel opens or closes.
        /// </summary>
        public event ChatInputBoxElement.EmojiSelectionVisibilityChangedDelegate EmojiSelectionVisibilityChanged;

        /// <summary>
        ///     Raised whenever the user attempts to send the content of the input box as a chat message.
        /// </summary>
        public event ChatInputBoxElement.InputSubmittedDelegate InputSubmitted;

        /// <summary>
        ///     Raised when the option to change the visibility of the chat bubbles over the avatar changes its value.
        /// </summary>
        public event Action<bool>? ChatBubbleVisibilityChanged;

        public void Initialize(IReadOnlyList<ChatMessage> chatMessages, bool areChatBubblesVisible)
        {
            closeChatButton.onClick.AddListener(CloseChat);
            loopList.InitListView(0, OnGetItemByIndex);

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

            SetMessages(chatMessages);
        }

        private void OnInputBoxSelectionChanged(bool isSelected)
        {
            if (isSelected)
            {
                if (isChatClosed)
                {
                    isChatClosed = false;
                    ToggleChat(true);
                    ShowLastMessage();
                }

                if (isInputSelected) return;

                isInputSelected = true;
                StopChatEntriesFadeout();
                InputBoxFocusChanged?.Invoke(true);
            }
            else
            {
                isInputSelected = false;
                StartChatEntriesFadeout();
                InputBoxFocusChanged?.Invoke(false);
            }
        }

        /// <summary>
        ///     Replaces the message data of the view with another.
        /// </summary>
        /// <param name="messages">The new messages to display in the view.</param>
        public void SetMessages(IReadOnlyList<ChatMessage> messages)
        {
            chatMessages = messages;
        }

        /// <summary>
        ///     Opens or closes the chat window.
        /// </summary>
        /// <param name="show">Whether to open or close it.</param>
        public void ToggleChat(bool show)
        {
            panelBackgroundCanvasGroup.gameObject.SetActive(show);
            loopList.gameObject.SetActive(show);

            if (!show) // Note: This is necessary to avoid items animating when re-opening the chat window
                entriesPendingToAnimate = 0;
        }

        /// <summary>
        ///     Makes the input box stop receiving user inputs.
        /// </summary>
        public void DisableInputBoxSubmissions()
        {
            chatInputBox.DisableInputBoxSubmissions();
        }

        /// <summary>
        ///     Makes the input box start receiving user inputs.
        /// </summary>
        public void EnableInputBoxSubmissions()
        {
            chatInputBox.EnableInputBoxSubmissions();
        }

        /// <summary>
        ///     Moves the chat so it shows the last created message.
        /// </summary>
        public void ShowLastMessage()
        {
            loopList.MovePanelToItemIndex(0, 0);
        }

        /// <summary>
        ///     Makes sure the chat window is showing all the messages stored in the data.
        /// </summary>
        public void RefreshMessages()
        {
            ResetChatEntriesFadeout();

            entriesPendingToAnimate = chatMessages.Count - loopList.ItemTotalCount;

            if (entriesPendingToAnimate < 0)
                entriesPendingToAnimate = 0;

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
        ///     Makes the input box gain the focus. It does not modify its content.
        /// </summary>
        public void FocusInputBox()
        {
            chatInputBox.FocusInputBox();
        }

        /// <summary>
        ///     Makes the input box gain the focus and replaces its content.
        /// </summary>
        /// <param name="text">The new content of the input box.</param>
        public void FocusInputBoxWithText(string text)
        {
            chatInputBox.FocusInputBoxWithText(text);
        }

        /// <summary>
        ///     Makes the chat submit the current content of the input box.
        /// </summary>
        public void SubmitInput()
        {
            chatInputBox.SubmitInput();
        }

        /// <summary>
        ///     Performs a click event on the chat window.
        /// </summary>
        public void Click()
        {
            chatInputBox.Click();
        }

        /// <summary>
        ///     Plays the sound FX of the chat receiving a new message.
        /// </summary>
        public void PlayMessageReceivedSfx()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(chatReceiveMessageAudio);
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
                    itemData.SentByOwnUser ? listView.ItemPrefabDataList[(int)ChatItemPrefabIndex.ChatEntryOwn].mItemPrefab.name : listView.ItemPrefabDataList[(int)ChatItemPrefabIndex.ChatEntry].mItemPrefab.name);

                ChatEntryView itemScript = item!.GetComponent<ChatEntryView>()!;
                SetItemData(index, itemData, itemScript);

                Button? messageOptionsButton = itemScript.messageBubbleElement.messageOptionsButton;
                itemScript.messageBubbleElement.SetupHyperlinkHandlerDependencies(viewDependencies);
                messageOptionsButton?.onClick.RemoveAllListeners();

                messageOptionsButton?.onClick.AddListener(() =>
                    OnChatMessageOptionsButtonClicked(itemData.Message, itemScript));
            }

            return item;
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

            // Views that correspond to new added items have to be animated
            if (index - 1 < entriesPendingToAnimate) // Note: -1 because the first real message starts at 1, which is the latest messaged added
                itemView.AnimateChatEntry();
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

        private void OnInputChanged(string inputText)
        {
            StopChatEntriesFadeout();
        }

        private void OnEmojiSelectionVisibilityChanged(bool isVisible)
        {
            EmojiSelectionVisibilityChanged?.Invoke(isVisible);
        }

        private void OnInputSubmitted(string messageToSend, string origin)
        {
            InputSubmitted?.Invoke(messageToSend, origin);
        }

        private void OnToggleChatBubblesValueChanged(bool isToggled)
        {
            ChatBubbleVisibilityChanged?.Invoke(isToggled);
        }

        /// <summary>
        ///     The prefab to use when instantiating a new item.
        /// </summary>
        private enum ChatItemPrefabIndex // It must match the list in the LoopListView.
        {
            ChatEntry,
            ChatEntryOwn,
            Padding,
            SystemChatEntry,
        }
    }
}
