using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.CharacterCamera;
using DCL.Chat.Commands;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Emoji;
using DCL.Input;
using DCL.Input.Component;
using DCL.Input.Systems;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using ECS.Abstract;
using MVC;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Utility;
using Utility.Arch;

namespace DCL.Chat
{
    public class ChatController : ControllerBase<ChatView>
    {
        private const int MAX_MESSAGE_LENGTH = 250;
        private const string EMOJI_SUGGESTION_PATTERN = @":\w+";
        private const string EMOJI_TAG = "[emoji]";
        private const string HASH_CHARACTER = "#";
        private const string ORIGIN = "chat";
        private static readonly Regex EMOJI_PATTERN_REGEX = new (EMOJI_SUGGESTION_PATTERN, RegexOptions.Compiled);

        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly ChatEntryConfigurationSO chatEntryConfiguration;
        private readonly IChatMessagesBus chatMessagesBus;
        private EmojiPanelController? emojiPanelController;
        private EmojiSuggestionPanel? emojiSuggestionPanelController;
        private readonly NametagsData nametagsData;
        private readonly EmojiPanelConfigurationSO emojiPanelConfiguration;
        private readonly TextAsset emojiMappingJson;
        private readonly EmojiSectionView emojiSectionViewPrefab;
        private readonly EmojiButton emojiButtonPrefab;
        private readonly EmojiSuggestionView emojiSuggestionViewPrefab;
        private readonly IChatHistory chatHistory;
        private readonly List<EmojiData> keysWithPrefix = new ();
        private readonly IEventSystem eventSystem;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly Mouse device;
        private readonly DCLInput dclInput;
        private readonly ChatCommandsHandler commandsHandler;
        private readonly IInputBlock inputBlock;

        private CancellationTokenSource cts;
        private CancellationTokenSource emojiPanelCts;
        private SingleInstanceEntity cameraEntity;
        private (IChatCommand command, Match param) chatCommand;
        private bool isChatClosed;
        private bool isInputSelected;
        private IReadOnlyList<RaycastResult> raycastResults;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public event Action<bool>? ChatBubbleVisibilityChanged;

        public ChatController(
            ViewFactoryMethod viewFactory,
            ChatEntryConfigurationSO chatEntryConfiguration,
            IChatMessagesBus chatMessagesBus,
            IChatHistory chatHistory,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            NametagsData nametagsData,
            EmojiPanelConfigurationSO emojiPanelConfiguration,
            TextAsset emojiMappingJson,
            EmojiSectionView emojiSectionViewPrefab,
            EmojiButton emojiButtonPrefab,
            EmojiSuggestionView emojiSuggestionViewPrefab,
            World world,
            Entity playerEntity,
            DCLInput dclInput,
            IEventSystem eventSystem,
            IInputBlock inputBlock
        ) : base(viewFactory)
        {
            this.chatEntryConfiguration = chatEntryConfiguration;
            this.chatMessagesBus = chatMessagesBus;
            this.chatHistory = chatHistory;
            this.entityParticipantTable = entityParticipantTable;
            this.nametagsData = nametagsData;
            this.emojiPanelConfiguration = emojiPanelConfiguration;
            this.emojiMappingJson = emojiMappingJson;
            this.emojiSectionViewPrefab = emojiSectionViewPrefab;
            this.emojiButtonPrefab = emojiButtonPrefab;
            this.emojiSuggestionViewPrefab = emojiSuggestionViewPrefab;
            this.world = world;
            this.playerEntity = playerEntity;
            this.dclInput = dclInput;
            this.eventSystem = eventSystem;
            this.inputBlock = inputBlock;

            device = InputSystem.GetDevice<Mouse>();
        }

        protected override void OnViewInstantiated()
        {
            cameraEntity = world.CacheCamera();

            //We start processing messages once the view is ready
            chatMessagesBus.MessageAdded += OnMessageAdded;
            chatHistory.OnMessageAdded += CreateChatEntry;
            chatHistory.OnCleared += ChatHistoryOnOnCleared;

            viewInstance!.OnChatViewPointerEnter += OnChatViewPointerEnter;
            viewInstance.OnChatViewPointerExit += OnChatViewPointerExit;
            viewInstance.CharacterCounter.SetMaximumLength(viewInstance.InputField.characterLimit);
            viewInstance.CharacterCounter.gameObject.SetActive(false);
            viewInstance.InputField.onValueChanged.AddListener(OnInputChanged);
            viewInstance.InputField.onSelect.AddListener(OnInputSelected);
            viewInstance.InputField.onDeselect.AddListener(OnInputDeselected);
            viewInstance.CloseChatButton.onClick.AddListener(CloseChat);
            viewInstance.LoopList.InitListView(0, OnGetItemByIndex);
            emojiPanelController = new EmojiPanelController(viewInstance.EmojiPanel, emojiPanelConfiguration, emojiMappingJson, emojiSectionViewPrefab, emojiButtonPrefab);
            emojiPanelController.OnEmojiSelected += AddEmojiToInput;

            emojiSuggestionPanelController = new EmojiSuggestionPanel(viewInstance.EmojiSuggestionPanel, emojiSuggestionViewPrefab, dclInput);
            emojiSuggestionPanelController.OnEmojiSelected += AddEmojiFromSuggestion;

            viewInstance.EmojiPanelButton.Button.onClick.AddListener(ToggleEmojiPanel);

            viewInstance.ChatBubblesToggle.Toggle.onValueChanged.AddListener(OnToggleChatBubblesValueChanged);
            viewInstance.ChatBubblesToggle.Toggle.SetIsOnWithoutNotify(nametagsData.showChatBubbles);
            OnToggleChatBubblesValueChanged(nametagsData.showChatBubbles);
            OnFocus();
        }

        protected override void OnViewShow()
        {
            base.OnViewShow();
            dclInput.UI.Click.performed += OnClick;
            dclInput.Shortcuts.ToggleNametags.performed += ToggleNametagsFromShortcut;
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();
            dclInput.UI.Click.performed -= OnClick;
            dclInput.Shortcuts.ToggleNametags.performed -= ToggleNametagsFromShortcut;
        }

        private void OnClick(InputAction.CallbackContext obj)
        {
            CheckIfClickedOnEmojiPanel();

            void CheckIfClickedOnEmojiPanel()
            {
                if (! (viewInstance!.EmojiPanel.gameObject.activeInHierarchy ||
                       viewInstance.EmojiSuggestionPanel.gameObject.activeInHierarchy)) return;

                raycastResults = eventSystem.RaycastAll(device.position.value);
                var clickedOnPanel = false;

                foreach (RaycastResult result in raycastResults)
                {
                    if (result.gameObject == viewInstance!.EmojiPanel.gameObject ||
                        result.gameObject == viewInstance.EmojiSuggestionPanel.ScrollView.gameObject ||
                        result.gameObject == viewInstance.EmojiPanelButton.gameObject) { clickedOnPanel = true; }
                }

                if (!clickedOnPanel)
                {
                    if (viewInstance!.EmojiPanel.gameObject.activeInHierarchy)
                    {
                        viewInstance!.EmojiPanelButton.SetState(false);
                        viewInstance.EmojiPanel.gameObject.SetActive(false);
                        EnableUnwantedInputs();
                    }
                    emojiSuggestionPanelController!.SetPanelVisibility(false);
                }
            }
        }

        private void OnChatViewPointerExit() =>
            world.TryRemove<CameraBlockerComponent>(cameraEntity);

        private void OnChatViewPointerEnter() =>
            world.AddOrGet(cameraEntity, new CameraBlockerComponent());

        private void AddEmojiFromSuggestion(string emojiCode, bool shouldClose)
        {
            if (viewInstance!.InputField.text.Length >= MAX_MESSAGE_LENGTH)
                return;

            UIAudioEventsBus.Instance.SendPlayAudioEvent(viewInstance.AddEmojiAudio);
            viewInstance.InputField.SetTextWithoutNotify(viewInstance.InputField.text.Replace(EMOJI_PATTERN_REGEX.Match(viewInstance.InputField.text).Value, emojiCode));
            viewInstance.InputField.stringPosition += emojiCode.Length;
            viewInstance.InputField.ActivateInputField();

            if (shouldClose)
                emojiSuggestionPanelController!.SetPanelVisibility(false);
        }

        private void ToggleNametagsFromShortcut(InputAction.CallbackContext obj)
        {
            nametagsData.showNameTags = !nametagsData.showNameTags;

            if (!nametagsData.showNameTags)
            {
                viewInstance!.ChatBubblesToggle.OffImage.gameObject.SetActive(true);
                viewInstance.ChatBubblesToggle.OnImage.gameObject.SetActive(false);
            }
            else
            {
                viewInstance!.ChatBubblesToggle.OffImage.gameObject.SetActive(!nametagsData.showChatBubbles);
                viewInstance.ChatBubblesToggle.OnImage.gameObject.SetActive(nametagsData.showChatBubbles);
            }
        }

        private void OnToggleChatBubblesValueChanged(bool isToggled)
        {
            if (!nametagsData.showNameTags)
                return;

            viewInstance!.ChatBubblesToggle.OffImage.gameObject.SetActive(!isToggled);
            viewInstance.ChatBubblesToggle.OnImage.gameObject.SetActive(isToggled);
            nametagsData.showChatBubbles = isToggled;

            ChatBubbleVisibilityChanged?.Invoke(isToggled);
        }

        private void AddEmojiToInput(string emoji)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(viewInstance!.AddEmojiAudio);

            if (viewInstance.InputField.text.Length >= MAX_MESSAGE_LENGTH)
                return;

            int caretPosition = viewInstance.InputField.stringPosition;
            viewInstance.InputField.text = viewInstance.InputField.text.Insert(caretPosition, EMOJI_TAG);
            viewInstance.InputField.text = viewInstance.InputField.text.Replace(EMOJI_TAG, emoji);
            viewInstance.InputField.stringPosition += emoji.Length;

            viewInstance.InputField.ActivateInputField();
        }

        private void ToggleEmojiPanel()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(viewInstance!.OpenEmojiPanelAudio);

            emojiPanelCts = emojiPanelCts.SafeRestart();
            bool toggle = !viewInstance.EmojiPanel.gameObject.activeInHierarchy;
            viewInstance.EmojiPanel.gameObject.SetActive(toggle);
            viewInstance.EmojiPanelButton.SetState(toggle);
            emojiSuggestionPanelController!.SetPanelVisibility(false);
            viewInstance.EmojiPanel.EmojiContainer.gameObject.SetActive(toggle);
            viewInstance.InputField.ActivateInputField();
            if (toggle) DisableUnwantedInputs();
            else EnableUnwantedInputs();
        }

        private void DisableUnwantedInputs()
        {
            world.AddOrGet(cameraEntity, new CameraBlockerComponent());
            inputBlock.Disable(InputMapComponent.Kind.CAMERA , InputMapComponent.Kind.SHORTCUTS , InputMapComponent.Kind.PLAYER);
        }

        private void EnableUnwantedInputs()
        {
            world.TryRemove<CameraBlockerComponent>(cameraEntity);
            inputBlock.Enable(InputMapComponent.Kind.CAMERA , InputMapComponent.Kind.SHORTCUTS , InputMapComponent.Kind.PLAYER);
        }

        private void OnSubmitAction(InputAction.CallbackContext obj)
        {
            if (emojiSuggestionPanelController is { IsActive: true }) return;
            if (viewInstance!.InputField.isFocused) return;

            viewInstance.InputField.OnSelect(null);
        }

        private void OnSubmit(string _)
        {
            if (emojiSuggestionPanelController is { IsActive: true })
            {
                emojiSuggestionPanelController.SetPanelVisibility(false);
                return;
            }

            if (viewInstance!.EmojiPanel.gameObject.activeInHierarchy)
            {
                viewInstance!.EmojiPanelButton.SetState(false);
                emojiPanelController!.SetPanelVisibility(false);
                EnableUnwantedInputs();
            }

            if (string.IsNullOrWhiteSpace(viewInstance!.InputField.text))
            {
                viewInstance.InputField.DeactivateInputField();
                viewInstance.InputField.OnDeselect(null);
                return;
            }

            UIAudioEventsBus.Instance.SendPlayAudioEvent(viewInstance.ChatSendMessageAudio);
            string messageToSend = viewInstance.InputField.text;

            viewInstance.InputField.text = string.Empty;
            viewInstance.InputField.ActivateInputField();

            chatMessagesBus.Send(messageToSend, ORIGIN);
        }

        private LoopListViewItem2? OnGetItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= chatHistory.Messages.Count)
                return null;

            ChatMessage itemData = chatHistory.Messages[index];
            LoopListViewItem2 item;

            if (itemData.IsPaddingElement)
                item = listView.NewListViewItem(listView.ItemPrefabDataList[2].mItemPrefab.name);
            else
            {
                item = listView.NewListViewItem(itemData.SystemMessage ? listView.ItemPrefabDataList[3].mItemPrefab.name : itemData.SentByOwnUser ? listView.ItemPrefabDataList[1].mItemPrefab.name : listView.ItemPrefabDataList[0].mItemPrefab.name);
                ChatEntryView itemScript = item!.GetComponent<ChatEntryView>()!;
                SetItemData(index, itemData, itemScript);
            }

            return item;
        }

        private void SetItemData(int index, ChatMessage itemData, ChatEntryView itemScript)
        {
            //temporary approach to extract the username without the walledId, will be refactored
            //once we have the proper integration of the profile retrieval
            Color playerNameColor = chatEntryConfiguration.GetNameColor(itemData.Sender.Contains(HASH_CHARACTER)
                ? $"{itemData.Sender.Substring(0, itemData.Sender.IndexOf(HASH_CHARACTER, StringComparison.Ordinal))}"
                : itemData.Sender);

            itemScript.playerName.color = playerNameColor;

            if (!itemData.SystemMessage)
            {
                itemScript.ProfileBackground.color = playerNameColor;
                playerNameColor.r += 0.3f;
                playerNameColor.g += 0.3f;
                playerNameColor.b += 0.3f;
                itemScript.ProfileOutline.color = playerNameColor;
            }

            itemScript.SetItemData(itemData);

            //Workaround needed to animate the chat entries due to infinite scroll plugin behaviour
            if (itemData.HasToAnimate)
            {
                itemScript.AnimateChatEntry();
                chatHistory.ForceUpdateMessage(index, new ChatMessage(itemData.Message, itemData.Sender, itemData.WalletAddress, itemData.SentByOwnUser, false));
            }
        }

        private void CloseChat()
        {
            isChatClosed = true;
            viewInstance!.ToggleChat(false);
        }

        private void OnInputDeselected(string inputText)
        {
            isInputSelected = false;
            viewInstance!.EmojiPanelButton.SetColor(false);
            viewInstance.CharacterCounter.gameObject.SetActive(false);
            viewInstance.StartChatEntriesFadeout();
            EnableUnwantedInputs();
        }

        private void OnInputSelected(string inputText)
        {
            if (isChatClosed)
            {
                isChatClosed = false;
                viewInstance!.ToggleChat(true);
                viewInstance.LoopList.MovePanelToItemIndex(0, 0);
            }

            UIAudioEventsBus.Instance.SendPlayAudioEvent(viewInstance!.EnterInputAudio);

            if (isInputSelected) return;

            isInputSelected = true;
            viewInstance.EmojiPanelButton.SetColor(true);
            viewInstance.CharacterCounter.gameObject.SetActive(true);
            viewInstance.StopChatEntriesFadeout();
            DisableUnwantedInputs();
        }

        private void OnInputChanged(string inputText)
        {
            HandleEmojiSearch(inputText);
            UIAudioEventsBus.Instance.SendPlayAudioEvent(viewInstance!.ChatInputTextAudio);

            viewInstance.CharacterCounter.SetCharacterCount(inputText.Length);
            viewInstance.StopChatEntriesFadeout();
        }

        protected override void OnBlur()
        {
            viewInstance!.InputField.onSubmit.RemoveAllListeners();
            dclInput.UI.Submit.performed -= OnSubmitAction;
            viewInstance.InputField.DeactivateInputField();
        }

        protected override void OnFocus()
        {
            viewInstance!.InputField.onSubmit.AddListener(OnSubmit);
            dclInput.UI.Submit.performed += OnSubmitAction;
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

                cts.SafeCancelAndDispose();
                cts = new CancellationTokenSource();

                SearchAndSetEmojiSuggestionsAsync(match.Value, cts.Token).Forget();
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
            emojiSuggestionPanelController.SetPanelVisibility(true);
        }

        private void OnMessageAdded(ChatMessage chatMessage)
        {
            chatHistory.AddMessage(chatMessage);
        }

        private void CreateChatEntry(ChatMessage chatMessage)
        {
            if (chatMessage.SentByOwnUser == false && entityParticipantTable.TryGet(chatMessage.WalletAddress, out IReadOnlyEntityParticipantTable.Entry entry))
            {
                Entity entity = entry.Entity;
                GenerateChatBubbleComponent(entity, chatMessage);
                UIAudioEventsBus.Instance.SendPlayAudioEvent(viewInstance!.ChatReceiveMessageAudio);
            }
            else if (chatMessage is {SystemMessage: false, SentByOwnUser: true })
                GenerateChatBubbleComponent(playerEntity, chatMessage);

            viewInstance!.ResetChatEntriesFadeout();

            viewInstance.LoopList.SetListItemCount(chatHistory.Messages.Count, false);
            viewInstance.LoopList.MovePanelToItemIndex(0, 0);
        }

        private void GenerateChatBubbleComponent(Entity e, ChatMessage chatMessage)
        {
            if(nametagsData is {showChatBubbles: true, showNameTags: true })
                world.AddOrGet(e, new ChatBubbleComponent(chatMessage.Message, chatMessage.Sender, chatMessage.WalletAddress));
        }

        private void ChatHistoryOnOnCleared()
        {
            viewInstance!.ResetChatEntriesFadeout();
            viewInstance.LoopList.SetListItemCount(chatHistory.Messages.Count);
            viewInstance.LoopList.MovePanelToItemIndex(0, 0);
        }

        public override void Dispose()
        {
            chatMessagesBus.MessageAdded -= CreateChatEntry;
            chatHistory.OnMessageAdded -= CreateChatEntry;
            chatHistory.OnCleared -= ChatHistoryOnOnCleared;

            if (emojiPanelController != null)
            {
                emojiPanelController.OnEmojiSelected -= AddEmojiToInput;
                emojiPanelController.Dispose();
            }

            if (emojiSuggestionPanelController != null)
                emojiSuggestionPanelController.OnEmojiSelected -= AddEmojiFromSuggestion;

            dclInput.UI.Submit.performed -= OnSubmitAction;
            cts.SafeCancelAndDispose();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
