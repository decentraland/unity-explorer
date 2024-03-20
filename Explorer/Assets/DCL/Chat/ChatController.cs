using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Emoji;
using DCL.Nametags;
using MVC;
using SuperScrollView;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Chat
{
    public partial class ChatController : ControllerBase<ChatView>
    {
        private const string EMOJI_SUGGESTION_PATTERN = @":\w+";

        private static readonly Regex EMOJI_PATTERN_REGEX = new (EMOJI_SUGGESTION_PATTERN);

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
        private World world;

        private string currentMessage = string.Empty;
        private CancellationTokenSource cts;
        private CancellationTokenSource emojiPanelCts;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public ChatController(
            ViewFactoryMethod viewFactory,
            ChatEntryConfigurationSO chatEntryConfiguration,
            IChatMessagesBus chatMessagesBus,
            NametagsData nametagsData,
            EmojiPanelConfigurationSO emojiPanelConfiguration,
            TextAsset emojiMappingJson,
            EmojiSectionView emojiSectionViewPrefab,
            EmojiButton emojiButtonPrefab,
            EmojiSuggestionView emojiSuggestionViewPrefab,
            World world) : base(viewFactory)
        {
            this.chatEntryConfiguration = chatEntryConfiguration;
            this.chatMessagesBus = chatMessagesBus;
            this.nametagsData = nametagsData;
            this.emojiPanelConfiguration = emojiPanelConfiguration;
            this.emojiMappingJson = emojiMappingJson;
            this.emojiSectionViewPrefab = emojiSectionViewPrefab;
            this.emojiButtonPrefab = emojiButtonPrefab;
            this.emojiSuggestionViewPrefab = emojiSuggestionViewPrefab;
            this.world = world;

            chatMessagesBus.OnMessageAdded += CreateChatEntry;
        }

        protected override void OnViewInstantiated()
        {
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
            OnToggleChatBubblesValueChanged(nametagsData.showChatBubbles);
        }

        private void AddEmojiFromSuggestion(string emojiCode)
        {
            viewInstance.InputField.text = viewInstance.InputField.text.Replace(EMOJI_PATTERN_REGEX.Match(viewInstance.InputField.text).Value, emojiCode);
            viewInstance.InputField.ActivateInputField();
            viewInstance.InputField.caretPosition = viewInstance.InputField.text.Length;
        }

        private void OnToggleChatBubblesValueChanged(bool isToggled)
        {
            viewInstance.ChatBubblesToggle.OffImage.gameObject.SetActive(!isToggled);
            viewInstance.ChatBubblesToggle.OnImage.gameObject.SetActive(isToggled);
            nametagsData.showChatBubbles = isToggled;
        }

        private void AddEmojiToInput(string emoji)
        {
            int caretPosition = viewInstance.InputField.caretPosition;
            viewInstance.InputField.text = viewInstance.InputField.text.Insert(caretPosition, emoji);
            viewInstance.InputField.ActivateInputField();
        }

        private void ToggleEmojiPanel()
        {
            emojiPanelCts.SafeCancelAndDispose();
            emojiPanelCts = new CancellationTokenSource();
            viewInstance.EmojiPanel.gameObject.SetActive(!viewInstance.EmojiPanel.gameObject.activeInHierarchy);
            ToggleEmojiPanelAsync(emojiPanelCts.Token).Forget();
        }

        private UniTask ToggleEmojiPanelAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            viewInstance.EmojiPanel.EmojiContainer.gameObject.SetActive(viewInstance.EmojiPanel.gameObject.activeInHierarchy);
            viewInstance.InputField.ActivateInputField();
            return UniTask.CompletedTask;
        }

        private void OnSubmit(string _)
        {
            if (string.IsNullOrWhiteSpace(currentMessage))
                return;

            chatMessagesBus.Send(currentMessage);
            currentMessage = string.Empty;
            viewInstance.InputField.text = string.Empty;
            emojiPanelController.SetPanelVisibility(false);
            viewInstance.InputField.ActivateInputField();
        }

        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= chatMessages.Count)
                return null;

            ChatMessage itemData = chatMessages[index];

            LoopListViewItem2 item = listView.NewListViewItem(itemData.SentByOwnUser ? listView.ItemPrefabDataList[1].mItemPrefab.name : listView.ItemPrefabDataList[0].mItemPrefab.name);

            ChatEntryView itemScript = item.GetComponent<ChatEntryView>();
            itemScript.playerName.color = itemData.SentByOwnUser ? Color.white : chatEntryConfiguration.GetNameColor(itemData.Sender);
            itemScript.SetItemData(itemData);

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
        }

        private void OnInputSelected(string inputText)
        {
            viewInstance.CharacterCounter.gameObject.SetActive(true);
            viewInstance.StopChatEntriesFadeout();
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
            else
            {
                emojiSuggestionPanelController.SetPanelVisibility(false);
            }
        }

        private async UniTaskVoid SearchAndSetEmojiSuggestionsAsync(string value, CancellationToken ct)
        {
            await DictionaryUtils.GetKeysWithPrefixAsync(emojiPanelController.EmojiNameMapping, value, keysWithPrefix, ct);

            emojiSuggestionPanelController.SetValues(keysWithPrefix);
            emojiSuggestionPanelController.SetPanelVisibility(true);
        }

        private void CreateChatEntry(ChatMessage chatMessage)
        {
            world.Create(new ChatBubbleComponent(chatMessage.Message, chatMessage.Sender, chatMessage.WalletAddress));
            viewInstance.ResetChatEntriesFadeout();
            chatMessages.Add(chatMessage);
            viewInstance.LoopList.SetListItemCount(chatMessages.Count, false);
            viewInstance.LoopList.MovePanelToItemIndex(chatMessages.Count - 1, 0);
        }

        public override void Dispose()
        {
            chatMessagesBus.OnMessageAdded -= CreateChatEntry;
            emojiPanelController.OnEmojiSelected -= AddEmojiToInput;
            emojiSuggestionPanelController.OnEmojiSelected -= AddEmojiFromSuggestion;
            emojiPanelController.Dispose();
            cts.SafeCancelAndDispose();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
