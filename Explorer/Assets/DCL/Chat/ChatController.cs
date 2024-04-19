using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.Emoji;
using DCL.Input;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using DCL.Profiles;
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

namespace DCL.Chat
{
    public class ChatController : ControllerBase<ChatView>
    {
        private const int MAX_MESSAGE_LENGTH = 250;

        private const string EMOJI_SUGGESTION_PATTERN = @":\w+";
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
        private readonly List<ChatMessage> chatMessages = new ();
        private readonly List<EmojiData> keysWithPrefix = new ();
        private readonly IEventSystem eventSystem;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly Mouse device;
        private readonly DCLInput dclInput;
        private readonly ChatCommandsHandler commandsHandler;

        private CancellationTokenSource cts;
        private CancellationTokenSource emojiPanelCts;
        private SingleInstanceEntity cameraEntity;
        private (IChatCommand command, Match param) chatCommand;
        private CancellationTokenSource commandCts = new ();
        private bool isChatClosed = false;
        private IReadOnlyList<RaycastResult> raycastResults;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public ChatController(
            ViewFactoryMethod viewFactory,
            ChatEntryConfigurationSO chatEntryConfiguration,
            IChatMessagesBus chatMessagesBus,
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
            IEventSystem eventSystem
        ) : base(viewFactory)
        {
            this.chatEntryConfiguration = chatEntryConfiguration;
            this.chatMessagesBus = chatMessagesBus;
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

            chatMessagesBus.OnMessageAdded += CreateChatEntry;
            // Adding two elements to count as top and bottom padding
            chatMessages.Add(new ChatMessage(true));
            chatMessages.Add(new ChatMessage(true));
            device = InputSystem.GetDevice<Mouse>();
        }

        protected override void OnViewInstantiated()
        {
            cameraEntity = world.CacheCamera();
            viewInstance.OnChatViewPointerEnter += OnChatViewPointerEnter;
            viewInstance.OnChatViewPointerExit += OnChatViewPointerExit;
            viewInstance.CharacterCounter.SetMaximumLength(viewInstance.InputField.characterLimit);
            viewInstance.CharacterCounter.gameObject.SetActive(false);
            viewInstance.InputField.onValueChanged.AddListener(OnInputChanged);
            viewInstance.InputField.onSelect.AddListener(OnInputSelected);
            viewInstance.InputField.onDeselect.AddListener(OnInputDeselected);
            viewInstance.InputField.onSubmit.AddListener(OnSubmit);
            viewInstance.CloseChatButton.onClick.AddListener(CloseChat);
            viewInstance.LoopList.InitListView(0, OnGetItemByIndex);
            emojiPanelController = new EmojiPanelController(viewInstance.EmojiPanel, emojiPanelConfiguration, emojiMappingJson, emojiSectionViewPrefab, emojiButtonPrefab);
            emojiPanelController.OnEmojiSelected += AddEmojiToInput;

            emojiSuggestionPanelController = new EmojiSuggestionPanel(viewInstance.EmojiSuggestionPanel, emojiSuggestionViewPrefab, dclInput);
            emojiSuggestionPanelController.OnEmojiSelected += AddEmojiFromSuggestion;

            viewInstance.EmojiPanelButton.Button.onClick.AddListener(ToggleEmojiPanel);

            viewInstance.ChatBubblesToggle.Toggle.onValueChanged.AddListener(OnToggleChatBubblesValueChanged);
            viewInstance.ChatBubblesToggle.Toggle.SetIsOnWithoutNotify(nametagsData.showChatBubbles);
            dclInput.UI.Submit.performed += OnSubmitAction;
            OnToggleChatBubblesValueChanged(nametagsData.showChatBubbles);
        }

        protected override void OnViewShow()
        {
            base.OnViewShow();
            dclInput.UI.Click.performed += OnClick;
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();
            dclInput.UI.Click.performed -= OnClick;
        }

        private void OnClick(InputAction.CallbackContext obj)
        {
            raycastResults = eventSystem.RaycastAll(device.position.value);
            var clickedOnPanel = false;
            foreach (RaycastResult raycasted in raycastResults)
                if (raycasted.gameObject == viewInstance.EmojiPanel.gameObject || raycasted.gameObject == viewInstance.EmojiSuggestionPanel.gameObject)
                    clickedOnPanel = true;

            if (!clickedOnPanel)
            {
                viewInstance.EmojiPanelButton.SetState(false);
                viewInstance.EmojiPanel.gameObject.SetActive(false);
                emojiSuggestionPanelController!.SetPanelVisibility(false);
            }
        }

        private void OnChatViewPointerExit() =>
            world.Remove<CameraBlockerComponent>(cameraEntity);

        private void OnChatViewPointerEnter() =>
            world.AddOrGet(cameraEntity, new CameraBlockerComponent());

        private void AddEmojiFromSuggestion(string emojiCode, bool shouldClose)
        {
            if (viewInstance.InputField.text.Length >= MAX_MESSAGE_LENGTH)
                return;

            UIAudioEventsBus.Instance.SendPlayAudioEvent(viewInstance.AddEmojiAudio);
            viewInstance.InputField.SetTextWithoutNotify(viewInstance.InputField.text.Replace(EMOJI_PATTERN_REGEX.Match(viewInstance.InputField.text).Value, emojiCode));
            viewInstance.InputField.stringPosition += emojiCode.Length;
            viewInstance.InputField.ActivateInputField();
            if(shouldClose)
                emojiSuggestionPanelController!.SetPanelVisibility(false);
        }

        private void OnToggleChatBubblesValueChanged(bool isToggled)
        {
            viewInstance.ChatBubblesToggle.OffImage.gameObject.SetActive(!isToggled);
            viewInstance.ChatBubblesToggle.OnImage.gameObject.SetActive(isToggled);
            nametagsData.showChatBubbles = isToggled;
        }

        private void AddEmojiToInput(string emoji)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(viewInstance.AddEmojiAudio);

            if (viewInstance.InputField.text.Length >= MAX_MESSAGE_LENGTH)
                return;

            int caretPosition = viewInstance.InputField.stringPosition;
            viewInstance.InputField.text = viewInstance.InputField.text.Insert(caretPosition, "[emoji]");
            viewInstance.InputField.text = viewInstance.InputField.text.Replace("[emoji]", emoji);
            viewInstance.InputField.stringPosition += emoji.Length;

            viewInstance.InputField.ActivateInputField();
        }

        private void ToggleEmojiPanel()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(viewInstance.OpenEmojiPanelAudio);
            emojiPanelCts.SafeCancelAndDispose();
            emojiPanelCts = new CancellationTokenSource();
            viewInstance.EmojiPanel.gameObject.SetActive(!viewInstance.EmojiPanel.gameObject.activeInHierarchy);
            viewInstance.EmojiPanelButton.SetState(viewInstance.EmojiPanel.gameObject.activeInHierarchy);
            emojiSuggestionPanelController!.SetPanelVisibility(false);
            ToggleEmojiPanelAsync(emojiPanelCts.Token).Forget();
        }

        private UniTask ToggleEmojiPanelAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            viewInstance.EmojiPanel.EmojiContainer.gameObject.SetActive(viewInstance.EmojiPanel.gameObject.activeInHierarchy);

            if (viewInstance.EmojiPanel.EmojiContainer.gameObject.activeInHierarchy)
                BlockPlayerMovement();
            else
                UnblockPlayerMovement();

            viewInstance.InputField.ActivateInputField();
            return UniTask.CompletedTask;
        }

        private void BlockPlayerMovement()
        {
            world.AddOrGet(playerEntity, new MovementBlockerComponent());
            dclInput.Shortcuts.Disable();
        }

        private void UnblockPlayerMovement()
        {
            world.Remove<MovementBlockerComponent>(playerEntity);
            dclInput.Shortcuts.Enable();
        }

        private void OnSubmitAction(InputAction.CallbackContext obj)
        {
            if (emojiSuggestionPanelController is { IsActive: true }) return;
            if (viewInstance.InputField.isFocused) return;

            viewInstance.InputField.ActivateInputField();
            viewInstance.InputField.OnSelect(null);
        }

        private void OnSubmit(string _)
        {
            if (emojiSuggestionPanelController is { IsActive: true })
            {
                emojiSuggestionPanelController.SetPanelVisibility(false);
                return;
            }
            emojiPanelController.SetPanelVisibility(false);

            if (string.IsNullOrWhiteSpace(viewInstance.InputField.text))
            {
                viewInstance.InputField.DeactivateInputField();
                viewInstance.InputField.OnDeselect(null);
                return;
            }

            UIAudioEventsBus.Instance.SendPlayAudioEvent(viewInstance.ChatSendMessageAudio);
            string messageToSend = viewInstance.InputField.text;

            viewInstance.InputField.text = string.Empty;
            viewInstance.InputField.ActivateInputField();
            emojiSuggestionPanelController.SetPanelVisibility(false);

            chatMessagesBus.Send(messageToSend);
        }

        private LoopListViewItem2? OnGetItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= chatMessages.Count)
                return null;

            ChatMessage itemData = chatMessages[index];
            LoopListViewItem2 item;

            if (itemData.IsPaddingElement)
                item = listView.NewListViewItem(listView.ItemPrefabDataList[2].mItemPrefab.name);
            else
            {
                item = listView.NewListViewItem(itemData.SentByOwnUser ? listView.ItemPrefabDataList[1].mItemPrefab.name : listView.ItemPrefabDataList[0].mItemPrefab.name);
                ChatEntryView itemScript = item!.GetComponent<ChatEntryView>()!;

                if (entityParticipantTable.Has(itemData.WalletAddress))
                {
                    var entity = entityParticipantTable.Entity(itemData.WalletAddress);
                    Profile profile = world.Get<Profile>(entity);
                    if(profile.ProfilePicture != null)
                        itemScript.playerIcon.sprite = profile.ProfilePicture.Value.Asset;
                }

                //temporary approach to extract the username without the walledId, will be refactored
                //once we have the proper integration of the profile retrieval
                Color playerNameColor = chatEntryConfiguration.GetNameColor(itemData.Sender.Contains("#")
                    ? $"{itemData.Sender.Substring(0, itemData.Sender.IndexOf("#", StringComparison.Ordinal))}"
                    : itemData.Sender);

                itemScript.playerName.color = playerNameColor;
                itemScript.ProfileBackground.color = playerNameColor;
                playerNameColor.r += 0.3f;
                playerNameColor.g += 0.3f;
                playerNameColor.b += 0.3f;
                itemScript.ProfileOutline.color = playerNameColor;

                itemScript.SetItemData(itemData);

                //Workaround needed to animate the chat entries due to infinite scroll plugin behaviour
                if (itemData.HasToAnimate)
                {
                    itemScript.AnimateChatEntry();
                    chatMessages[index] = new ChatMessage(itemData.Message, itemData.Sender, itemData.WalletAddress, itemData.SentByOwnUser, false);
                }
            }

            return item;
        }

        private void CloseChat()
        {
            isChatClosed = true;
            viewInstance.ToggleChat(false);
        }

        private void OnInputDeselected(string inputText)
        {
            viewInstance.EmojiPanelButton.SetColor(false);
            viewInstance.CharacterCounter.gameObject.SetActive(false);
            viewInstance.StartChatEntriesFadeout();
            UnblockPlayerMovement();
        }

        private void OnInputSelected(string inputText)
        {
            if (isChatClosed)
            {
                isChatClosed = false;
                viewInstance.ToggleChat(true);
                viewInstance.LoopList.MovePanelToItemIndex(0, 0);
            }

            viewInstance.EmojiPanelButton.SetColor(true);
            viewInstance.CharacterCounter.gameObject.SetActive(true);
            viewInstance.StopChatEntriesFadeout();
            BlockPlayerMovement();
        }

        private void OnInputChanged(string inputText)
        {
            HandleEmojiSearch(inputText);
            UIAudioEventsBus.Instance.SendPlayAudioEvent(viewInstance.ChatInputTextAudio);

            viewInstance.CharacterCounter.SetCharacterCount(inputText.Length);
            viewInstance.StopChatEntriesFadeout();
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
                if(emojiSuggestionPanelController is { IsActive: true })
                    emojiSuggestionPanelController!.SetPanelVisibility(false);
            }
        }

        private async UniTaskVoid SearchAndSetEmojiSuggestionsAsync(string value, CancellationToken ct)
        {
            await DictionaryUtils.GetKeysWithPrefixAsync(emojiPanelController.EmojiNameMapping, value, keysWithPrefix, ct);

            emojiSuggestionPanelController!.SetValues(keysWithPrefix);
            emojiSuggestionPanelController.SetPanelVisibility(true);
        }

        private void CreateChatEntry(ChatMessage chatMessage)
        {
            if (chatMessage.SentByOwnUser == false && entityParticipantTable.Has(chatMessage.WalletAddress))
            {
                Entity entity = entityParticipantTable.Entity(chatMessage.WalletAddress);
                world.AddOrGet(entity, new ChatBubbleComponent(chatMessage.Message, chatMessage.Sender, chatMessage.WalletAddress));
                UIAudioEventsBus.Instance.SendPlayAudioEvent(viewInstance.ChatReceiveMessageAudio);
            }
            else
            {
                world.AddOrGet(playerEntity, new ChatBubbleComponent(chatMessage.Message, chatMessage.Sender, chatMessage.WalletAddress));
            }

            viewInstance.ResetChatEntriesFadeout();

            //Removing padding element and reversing list due to infinite scroll view behaviour
            chatMessages.Remove(chatMessages[^1]);
            chatMessages.Reverse();
            chatMessages.Add(chatMessage);
            chatMessages.Add(new ChatMessage(true));
            chatMessages.Reverse();

            viewInstance.LoopList.SetListItemCount(chatMessages.Count, false);
            viewInstance.LoopList.MovePanelToItemIndex(0, 0);
        }

        public override void Dispose()
        {
            chatMessagesBus.OnMessageAdded -= CreateChatEntry;

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
