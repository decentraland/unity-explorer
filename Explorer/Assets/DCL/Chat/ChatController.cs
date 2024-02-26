using Cysharp.Threading.Tasks;
using MVC;
using System;
using System.Threading;
using Object = UnityEngine.Object;

namespace DCL.Chat
{
    public partial class ChatController : ControllerBase<ChatView>
    {
        private readonly ChatEntryView chatEntryView;
        private readonly ChatEntryConfigurationSO chatEntryConfiguration;
        private readonly IChatMessagesBus chatMessagesBus;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public ChatController(
            ViewFactoryMethod viewFactory,
            ChatEntryView chatEntryView,
            ChatEntryConfigurationSO chatEntryConfiguration,
            IChatMessagesBus chatMessagesBus) : base(viewFactory)
        {
            this.chatEntryView = chatEntryView;
            this.chatEntryConfiguration = chatEntryConfiguration;
            this.chatMessagesBus = chatMessagesBus;

            chatMessagesBus.OnMessageAdded += CreateChatEntry;
        }

        protected override void OnViewInstantiated()
        {
            viewInstance.CharacterCounter.SetMaximumLength(viewInstance.InputField.characterLimit);
            viewInstance.CharacterCounter.gameObject.SetActive(false);
            viewInstance.InputField.onValueChanged.AddListener(OnInputChanged);
            viewInstance.InputField.onSelect.AddListener(OnInputSelected);
            viewInstance.InputField.onDeselect.AddListener(OnInputDeselected);
            viewInstance.CloseChatButton.onClick.AddListener(CloseChat);
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
            viewInstance.ResetChatEntriesFadeout();
            //TODO: pool based on the virtual list pooling integration
            ChatEntryView entryView = Object.Instantiate(chatEntryView, viewInstance.MessagesContainer);
            entryView.AnimateChatEntry();
            entryView.Initialise(chatEntryConfiguration);
            entryView.SetUsername(chatMessage.Sender, chatMessage.WalletAddress);
            entryView.entryText.text = chatMessage.Message;
            entryView.SetSentByUser(chatMessage.SentByOwnUser);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
