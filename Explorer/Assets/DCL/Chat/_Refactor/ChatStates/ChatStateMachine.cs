using DCL.Chat.ChatMediator;
using DCL.Chat.ChatStates;
using DCL.Chat.EventBus;
using MVC;
using System;
using Utilities;

namespace DCL.Chat._Refactor.ChatStates
{
    public class ChatStateMachine : IDisposable
    {
        internal readonly IEventBus eventBus;
        private readonly ChatUIMediator mediator;
        private readonly ChatInputBlockingService inputBlocker;
        private readonly ChatClickDetectionService chatClickDetectionService;
        private readonly MVCStateMachine<ChatState, ChatStateContext> fsm;
        private readonly EventSubscriptionScope scope = new ();

        public ChatMainController MainController { get; }
        public bool IsInputFocused => fsm.CurrentState is FocusedChatState;
        public bool IsMinimized => fsm.CurrentState is MinimizedChatState;
        public bool IsHidden => fsm.CurrentState is HiddenChatState;

        public ChatStateMachine(
            IEventBus eventBus,
            ChatUIMediator mediator,
            ChatInputBlockingService inputBlocker,
            ChatClickDetectionService chatClickDetectionService,
            ChatMainController mainController)
        {
            this.eventBus = eventBus;
            this.mediator = mediator;
            this.inputBlocker = inputBlocker;
            this.chatClickDetectionService = chatClickDetectionService;

            MainController = mainController;

            var context = new ChatStateContext(mediator, inputBlocker);

            fsm = new MVCStateMachine<ChatState, ChatStateContext>(context, new InitChatState());
            fsm.AddState(new DefaultChatState());
            fsm.AddState(new FocusedChatState());
            fsm.AddState(new MembersChatState());
            fsm.AddState(new MinimizedChatState());
            fsm.AddState(new HiddenChatState());

            scope.Add(eventBus.Subscribe<ChatEvents.FocusRequestedEvent>(HandleFocusRequestedEvent));
            scope.Add(eventBus.Subscribe<ChatEvents.CloseChatEvent>(HandleCloseChatEvent));
            scope.Add(eventBus.Subscribe<ChatEvents.ToggleMembersEvent>(HandleToggleMembersEvent));

            chatClickDetectionService.OnClickInside += HandleClickInside;
            chatClickDetectionService.OnClickOutside += HandleClickOutside;
        }

        public void Dispose()
        {
            chatClickDetectionService.OnClickInside -= HandleClickInside;
            chatClickDetectionService.OnClickOutside -= HandleClickOutside;

            scope.Dispose();
        }

        public void OnViewShow()
        {
            fsm.ChangeState<DefaultChatState>();
        }

        private void HandleFocusRequestedEvent(ChatEvents.FocusRequestedEvent evt)
        {
            fsm.CurrentState.OnFocusRequested();
        }

        private void HandleCloseChatEvent(ChatEvents.CloseChatEvent evt)
        {
            fsm.CurrentState.OnCloseRequested();
        }

        private void HandleToggleMembersEvent(ChatEvents.ToggleMembersEvent evt)
        {
            fsm.CurrentState.OnToggleMembers();
        }

        private void HandleClickInside()
        {
            fsm.CurrentState.OnClickInside();
        }

        private void HandleClickOutside()
        {
            fsm.CurrentState.OnClickOutside();
        }

        public void SetInitialState(bool focus)
        {
            if (focus)
                fsm.ChangeState<FocusedChatState>();
            else
                fsm.ChangeState<DefaultChatState>();
        }

        public void Minimize()
        {
            fsm.CurrentState.OnMinimizeRequested();
        }

        /// <summary>
        ///     NOTE: this method is clunky,
        ///     NOTE: but it is used to set the visibility of the chat UI.
        /// </summary>
        /// <param name="isVisible"></param>
        public void SetVisibility(bool isVisible)
        {
            if (isVisible)
                fsm.ChangeState<DefaultChatState>();
            else
                fsm.ChangeState<HiddenChatState>();
        }
    }
}
