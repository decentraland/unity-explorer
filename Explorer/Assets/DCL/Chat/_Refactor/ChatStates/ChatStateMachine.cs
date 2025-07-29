using DCL.Chat.ChatMediator;
using DCL.Chat.ChatServices;
using DCL.Chat.ChatStates;
using DCL.Chat.EventBus;
using MVC;
using System;
using Utility;

namespace DCL.Chat._Refactor.ChatStates
{
    public class ChatStateMachine : IDisposable
    {
        private readonly ChatInputBlockingService inputBlocker;
        private readonly ChatClickDetectionService chatClickDetectionService;
        private readonly MVCStateMachine<ChatState, ChatStateContext> fsm;
        private readonly EventSubscriptionScope scope = new ();

        public ChatMainController MainController { get; }
        public bool IsFocused => fsm.CurrentState is FocusedChatState;
        public bool IsMinimized => fsm.CurrentState is MinimizedChatState;
        public bool IsHidden => fsm.CurrentState is HiddenChatState;

        public ChatStateMachine(
            IEventBus eventBus,
            ChatUIMediator mediator,
            ChatInputBlockingService inputBlocker,
            ChatClickDetectionService chatClickDetectionService,
            ChatMainController mainController)
        {
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

            MainController.PointerEntered += HandlePointerEntered;
            MainController.PointerExited += HandlePointerExited;
        }

        public void Dispose()
        {
            chatClickDetectionService.OnClickInside -= HandleClickInside;
            chatClickDetectionService.OnClickOutside -= HandleClickOutside;

            MainController.PointerEntered -= HandlePointerEntered;
            MainController.PointerExited -= HandlePointerExited;

            scope.Dispose();
        }

        public void OnViewShow()
        {
            inputBlocker.Initialize();

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

        private void HandlePointerExited()
        {
            fsm.CurrentState.OnPointerExit();
        }

        private void HandlePointerEntered()
        {
            fsm.CurrentState.OnPointerEnter();
        }

        public void Minimize()
        {
            fsm.CurrentState.OnMinimizeRequested();
        }

        public void SetInitialState(bool focus)
        {
            if (focus)
                fsm.ChangeState<FocusedChatState>();
            else
                fsm.ChangeState<DefaultChatState>();
        }

        /// <summary>
        /// NOTE: called from the SharedSpaceManager
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
