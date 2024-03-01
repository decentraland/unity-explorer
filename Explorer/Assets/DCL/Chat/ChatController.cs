using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Nametags;
using MVC;
using SuperScrollView;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Chat
{
    public partial class ChatController : ControllerBase<ChatView>
    {
        private readonly ChatEntryConfigurationSO chatEntryConfiguration;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly NametagsData nametagsData;
        private World world;

        private List<ChatMessage> chatMessages = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public ChatController(
            ViewFactoryMethod viewFactory,
            ChatEntryConfigurationSO chatEntryConfiguration,
            IChatMessagesBus chatMessagesBus,
            NametagsData nametagsData) : base(viewFactory)
        {
            this.chatEntryConfiguration = chatEntryConfiguration;
            this.chatMessagesBus = chatMessagesBus;
            this.nametagsData = nametagsData;

            chatMessagesBus.OnMessageAdded += CreateChatEntry;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder)
        {
            world = builder.World;
        }

        protected override void OnViewInstantiated()
        {
            viewInstance.CharacterCounter.SetMaximumLength(viewInstance.InputField.characterLimit);
            viewInstance.CharacterCounter.gameObject.SetActive(false);
            viewInstance.InputField.onValueChanged.AddListener(OnInputChanged);
            viewInstance.InputField.onSelect.AddListener(OnInputSelected);
            viewInstance.InputField.onDeselect.AddListener(OnInputDeselected);
            viewInstance.CloseChatButton.onClick.AddListener(CloseChat);
            viewInstance.LoopList.InitListView(0, OnGetItemByIndex);
            viewInstance.ChatBubblesToggle.Toggle.onValueChanged.AddListener(OnToggleChatBubblesValueChanged);
            viewInstance.ChatBubblesToggle.Toggle.SetIsOnWithoutNotify(nametagsData.showChatBubbles);
            OnToggleChatBubblesValueChanged(nametagsData.showChatBubbles);

        }

        private void OnToggleChatBubblesValueChanged(bool isToggled)
        {
            viewInstance.ChatBubblesToggle.OffImage.gameObject.SetActive(!isToggled);
            viewInstance.ChatBubblesToggle.OnImage.gameObject.SetActive(isToggled);
            nametagsData.showChatBubbles = isToggled;
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
            viewInstance.CharacterCounter.SetCharacterCount(inputText.Length);
            viewInstance.StopChatEntriesFadeout();
        }

        private void CreateChatEntry(ChatMessage chatMessage)
        {
            world.Create(new ChatBubbleComponent(chatMessage.Message, chatMessage.Sender, chatMessage.WalletAddress));
            viewInstance.ResetChatEntriesFadeout();
            chatMessages.Add(chatMessage);
            viewInstance.LoopList.SetListItemCount(chatMessages.Count, false);
            viewInstance.LoopList.MovePanelToItemIndex(chatMessages.Count-1, 0);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
