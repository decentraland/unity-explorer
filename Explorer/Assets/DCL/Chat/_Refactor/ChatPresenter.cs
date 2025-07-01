using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Input;
using DCL.UI.SharedSpaceManager;
using MVC;

namespace DCL.Chat
{
    public class ChatPresenter : ControllerBase<ChatMainView, ChatControllerShowParams>,
        IControllerInSharedSpace<ChatMainView, ChatControllerShowParams>
    {
        private readonly IChatHistory chatHistory;
        private readonly IChatMessagesBus chatMessagesBus;

        // put somewhere else?
        private IInputBlock inputBlock;
        private readonly IChatPresenterFactory presenterFactory;

        private ChatTitlebarPresenter? titleBarPresenter;
        private ChatConversationToolbarPresenter? conversationsPresenter;
        private ChatMemberListPresenter? memberListPresenter;
        private ChatMessageFeedPresenter? messageViewerPresenter;
        private ChatInputPresenter? chatInputPresenter;

        // chat update state to receive events and coordinate presenters


        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;
        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

        public ChatPresenter(ViewFactoryMethod viewFactory,
            IChatPresenterFactory presenterFactory,
            IChatHistory chatHistory,
            IChatMessagesBus chatMessagesBus,
            IInputBlock inputBlock) : base(viewFactory)
        {
            this.presenterFactory = presenterFactory;
            this.chatHistory = chatHistory;
            this.chatMessagesBus = chatMessagesBus;
            this.inputBlock = inputBlock;
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            // Create presenters
            conversationsPresenter = presenterFactory.CreateConversationList(viewInstance.ConversationToolbarView);
            memberListPresenter = presenterFactory.CreateMemberList(viewInstance.MemberListView);
            messageViewerPresenter = presenterFactory.CreateMessageFeed(viewInstance.MessageFeedView);
            chatInputPresenter = presenterFactory.CreateChatInput(viewInstance.InputView);
            titleBarPresenter = presenterFactory.CreateTitlebar(viewInstance.TitlebarView);

            // Wire up inter presenter communication
            conversationsPresenter!.OnConversationSelected += HandleConversationSelected;
            chatInputPresenter!.OnMessageSubmitted += HandleMessageSubmitted;
            chatInputPresenter!.OnFocusChanged += HandleInputFocusChanged;
            titleBarPresenter!.OnClosed += HandlePanelClosed;
            titleBarPresenter!.OnMemberListToggle += HandleMemberListToggled;
        }

        protected override void OnViewShow()
        {
            conversationsPresenter!.Enable();
            messageViewerPresenter!.Enable();
            chatInputPresenter!.Enable();
            titleBarPresenter!.Enable();
            memberListPresenter!.Disable(); // Starts disabled

            // The coordinator subscribes to global events
            chatMessagesBus.MessageAdded += HandleBusMessageAdded;

            // Set initial state
            HandleConversationSelected(ChatChannel.NEARBY_CHANNEL_ID.Id);
        }

        private void HandleBusMessageAdded(ChatChannel.ChannelId arg1, ChatMessage arg2)
        {
            // does whatever it should do through sub presenters
        }

        private string currentChannelId;
        private void HandleConversationSelected(string channelId)
        {
            // put into ChatUpdateState? or where the stage is stored
            if (currentChannelId == channelId) return;
            currentChannelId = channelId;

            var channel = chatHistory.Channels[new ChatChannel.ChannelId(channelId)];

            // Delegate work to the appropriate presenters
            messageViewerPresenter!.LoadChannel(channel);
            titleBarPresenter!.UpdateForChannel(channel);
            chatInputPresenter!.UpdateStateForChannel(channel);
            memberListPresenter!.LoadMembersForChannel(channel);
        }

        private void HandleMemberListToggled(bool active)
        {
        }

        private void HandlePanelClosed()
        {
        }

        private void HandleInputFocusChanged(bool isFocused)
        {
        }

        private void HandleMessageSubmitted(string message)
        {
        }

        // // missing
        // protected override void OnViewClose()
        // {
        //     // Unsubscribe from global events
        //     chatMessagesBus.MessageAdded -= HandleBusMessageAdded;
        //     
        //     // Dispose all presenters to clean them up
        //     conversationsPresenter?.Dispose();
        //     memberListPresenter?.Dispose();
        //     messageViewerPresenter?.Dispose();
        //     chatInputPresenter?.Dispose();
        //     titleBarPresenter?.Dispose();
        // }


        protected override void OnFocus()
        {
            base.OnFocus();
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();
            // clean up presenters
        }

        public override void Dispose()
        {
            base.Dispose();
        }


        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            // Wait for the close intent to be triggered
            var tcs = new UniTaskCompletionSource();
            titleBarPresenter!.OnClosed += () => tcs.TrySetResult();
            return tcs.Task;
        }


        public UniTask OnHiddenInSharedSpaceAsync(CancellationToken ct)
        {
            // Handle when the chat is hidden in shared space
            // This could be used to pause updates or save state
            return UniTask.CompletedTask;
        }
    }
}