using Arch.Core;
using Cysharp.Threading.Tasks;
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
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using Utility;

namespace DCL.Chat
{
    public partial class ChatController : ControllerBase<ChatView>
    {
        private const string EMOJI_SUGGESTION_PATTERN = @":\w+";
        private const int MAX_MESSAGE_LENGTH = 250;

        private static readonly Regex EMOJI_PATTERN_REGEX = new (EMOJI_SUGGESTION_PATTERN);

        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly ChatEntryConfigurationSO chatEntryConfiguration;
        private readonly IChatMessagesBus chatMessagesBus;
        private EmojiPanelController emojiPanelController;
        private EmojiSuggestionPanel emojiSuggestionPanelController;
        private readonly NametagsData nametagsData;
        private readonly EmojiPanelConfigurationSO emojiPanelConfiguration;
        private readonly TextAsset emojiMappingJson;
        private readonly EmojiSectionView emojiSectionViewPrefab;
        private readonly EmojiButton emojiButtonPrefab;
        private readonly EmojiSuggestionView emojiSuggestionViewPrefab;
        private readonly List<ChatMessage> chatMessages = new ();
        private readonly List<EmojiData> keysWithPrefix = new ();
        private readonly IEventSystem eventSystem;
        private World world;

        private string currentMessage = string.Empty;
        private CancellationTokenSource cts;
        private CancellationTokenSource emojiPanelCts;
        private readonly Entity playerEntity;
        private SingleInstanceEntity cameraEntity;
        private DCLInput dclInput;

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
            //Adding two elements to count as top and bottom padding
            chatMessages.Add(new ChatMessage(true));
            chatMessages.Add(new ChatMessage(true));
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

            emojiSuggestionPanelController = new EmojiSuggestionPanel(viewInstance.EmojiSuggestionPanel, emojiSuggestionViewPrefab);
            emojiSuggestionPanelController.OnEmojiSelected += AddEmojiFromSuggestion;

            viewInstance.EmojiPanelButton.onClick.AddListener(ToggleEmojiPanel);

            viewInstance.ChatBubblesToggle.Toggle.onValueChanged.AddListener(OnToggleChatBubblesValueChanged);
            viewInstance.ChatBubblesToggle.Toggle.SetIsOnWithoutNotify(nametagsData.showChatBubbles);
            dclInput.UI.Submit.performed += OnSubmitAction;
            OnToggleChatBubblesValueChanged(nametagsData.showChatBubbles);
        }

        private void OnChatViewPointerExit() =>
            world.Remove<CameraBlockerComponent>(cameraEntity);

        private void OnChatViewPointerEnter() =>
            world.AddOrGet(cameraEntity, new CameraBlockerComponent());

        private void AddEmojiFromSuggestion(string emojiCode)
        {
            if(viewInstance.InputField.text.Length >= MAX_MESSAGE_LENGTH)
                return;

            viewInstance.InputField.text = viewInstance.InputField.text.Replace(EMOJI_PATTERN_REGEX.Match(viewInstance.InputField.text).Value, emojiCode);
            viewInstance.InputField.stringPosition += emojiCode.Length;
            viewInstance.InputField.ActivateInputField();
        }

        private void OnToggleChatBubblesValueChanged(bool isToggled)
        {
            viewInstance.ChatBubblesToggle.OffImage.gameObject.SetActive(!isToggled);
            viewInstance.ChatBubblesToggle.OnImage.gameObject.SetActive(isToggled);
            nametagsData.showChatBubbles = isToggled;
        }

        private void AddEmojiToInput(string emoji)
        {
            if(viewInstance.InputField.text.Length >= MAX_MESSAGE_LENGTH)
                return;

            int caretPosition = viewInstance.InputField.stringPosition;
            viewInstance.InputField.text = viewInstance.InputField.text.Insert(caretPosition, "[emoji]");
            viewInstance.InputField.text = viewInstance.InputField.text.Replace("[emoji]", emoji);
            viewInstance.InputField.stringPosition += emoji.Length;

            viewInstance.InputField.ActivateInputField();
        }

        private void ToggleEmojiPanel()
        {
            emojiPanelCts.SafeCancelAndDispose();
            emojiPanelCts = new CancellationTokenSource();
            viewInstance.EmojiPanel.gameObject.SetActive(!viewInstance.EmojiPanel.gameObject.activeInHierarchy);
            emojiSuggestionPanelController.SetPanelVisibility(false);
            ToggleEmojiPanelAsync(emojiPanelCts.Token).Forget();
        }

        private UniTask ToggleEmojiPanelAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            viewInstance.EmojiPanel.EmojiContainer.gameObject.SetActive(viewInstance.EmojiPanel.gameObject.activeInHierarchy);

            if (viewInstance.EmojiPanel.EmojiContainer.gameObject.activeInHierarchy)
                world.AddOrGet(playerEntity, new MovementBlockerComponent());
            else
                world.Remove<MovementBlockerComponent>(playerEntity);

            viewInstance.InputField.ActivateInputField();
            return UniTask.CompletedTask;
        }

        private void OnSubmitAction(InputAction.CallbackContext obj)
        {
            if (viewInstance.InputField.isFocused) return;

            viewInstance.InputField.ActivateInputField();
            viewInstance.InputField.OnSelect(null);
        }

        private void OnSubmit(string _)
        {
            emojiPanelController.SetPanelVisibility(false);
            emojiSuggestionPanelController.SetPanelVisibility(false);

            if (string.IsNullOrWhiteSpace(currentMessage))
            {
                viewInstance.InputField.DeactivateInputField();
                viewInstance.InputField.OnDeselect(null);
                return;
            }

            chatMessagesBus.Send(currentMessage);
            currentMessage = string.Empty;
            viewInstance.InputField.text = string.Empty;
            viewInstance.InputField.ActivateInputField();
        }

        private LoopListViewItem2? OnGetItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= chatMessages.Count)
                return null;

            ChatMessage itemData = chatMessages[index];
            LoopListViewItem2 item;
            if(itemData.IsPaddingElement)
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
                itemScript.playerName.color = chatEntryConfiguration.GetNameColor(itemData.Sender);
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
            //TODO: will add logic for the panel closing once it's defined
        }

        private void OnInputDeselected(string inputText)
        {
            viewInstance.CharacterCounter.gameObject.SetActive(false);
            viewInstance.StartChatEntriesFadeout();
            world.Remove<MovementBlockerComponent>(playerEntity);
        }

        private void OnInputSelected(string inputText)
        {
            viewInstance.CharacterCounter.gameObject.SetActive(true);
            viewInstance.StopChatEntriesFadeout();
            world.AddOrGet(playerEntity, new MovementBlockerComponent());
        }

        private void OnInputChanged(string inputText)
        {
            HandleEmojiSearch(inputText);

            viewInstance.CharacterCounter.SetCharacterCount(inputText.Length);
            viewInstance.StopChatEntriesFadeout();
            currentMessage = inputText;
        }

        private void HandleEmojiSearch(string inputText)
        {
            Match match = EMOJI_PATTERN_REGEX.Match(inputText);

            if (match.Success)
            {
                if (match.Value.Length < 2)
                {
                    emojiSuggestionPanelController.SetPanelVisibility(false);
                    return;
                }

                cts.SafeCancelAndDispose();
                cts = new CancellationTokenSource();

                SearchAndSetEmojiSuggestionsAsync(match.Value, cts.Token).Forget();
            }
            else { emojiSuggestionPanelController.SetPanelVisibility(false); }
        }

        private async UniTaskVoid SearchAndSetEmojiSuggestionsAsync(string value, CancellationToken ct)
        {
            await DictionaryUtils.GetKeysWithPrefixAsync(emojiPanelController.EmojiNameMapping, value, keysWithPrefix, ct);

            emojiSuggestionPanelController.SetValues(keysWithPrefix);
            emojiSuggestionPanelController.SetPanelVisibility(true);
        }

        private void CreateChatEntry(ChatMessage chatMessage)
        {
            if (chatMessage.SentByOwnUser == false && entityParticipantTable.Has(chatMessage.WalletAddress))
            {
                var entity = entityParticipantTable.Entity(chatMessage.WalletAddress);
                world.AddOrGet(entity, new ChatBubbleComponent(chatMessage.Message, chatMessage.Sender, chatMessage.WalletAddress));
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
            emojiPanelController.OnEmojiSelected -= AddEmojiToInput;
            emojiSuggestionPanelController.OnEmojiSelected -= AddEmojiFromSuggestion;
            dclInput.UI.Submit.performed -= OnSubmitAction;
            emojiPanelController.Dispose();
            cts.SafeCancelAndDispose();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
