using DCL.Chat.ChatServices;
using MVC;
using System;
using Utility;

namespace DCL.Chat.ChatStates
{
    public class ChatStateMachine : IDisposable
    {
        private readonly ChatInputBlockingService inputBlocker;
        private readonly ChatClickDetectionService chatClickDetectionService;
        private readonly IEventBus eventBus;
        private readonly MVCStateMachine<ChatState, ChatStateContext> fsm;
        private readonly EventSubscriptionScope scope = new ();
        private readonly ChatPanelPresenter chatPanelPresenter;

        public bool IsFocused => fsm.CurrentState is FocusedChatState;
        public bool IsMinimized => fsm.CurrentState is MinimizedChatState;
        public bool IsHidden => fsm.CurrentState is HiddenChatState;

        public ChatStateMachine(
            IEventBus eventBus,
            ChatUIMediator mediator,
            ChatInputBlockingService inputBlocker,
            ChatClickDetectionService chatClickDetectionService,
            ChatPanelPresenter chatPanelPresenter)
        {
            this.inputBlocker = inputBlocker;
            this.chatClickDetectionService = chatClickDetectionService;
            this.eventBus = eventBus;

            this.chatPanelPresenter = chatPanelPresenter;

            var context = new ChatStateContext(mediator, inputBlocker);

            fsm = new MVCStateMachine<ChatState, ChatStateContext>(context, new InitChatState());
            fsm.AddState(new DefaultChatState());
            fsm.AddState(new FocusedChatState());
            fsm.AddState(new MembersChatState());
            fsm.AddState(new MinimizedChatState());
            fsm.AddState(new HiddenChatState());
            fsm.OnStateChanged += PropagateStateChange;

            scope.Add(eventBus.Subscribe<ChatEvents.FocusRequestedEvent>(HandleFocusRequestedEvent));
            scope.Add(eventBus.Subscribe<ChatEvents.CloseChatEvent>(HandleCloseChatEvent));
            scope.Add(eventBus.Subscribe<ChatEvents.ToggleMembersEvent>(HandleToggleMembersEvent));

            chatClickDetectionService.OnClickInside += HandleClickInside;
            chatClickDetectionService.OnClickOutside += HandleClickOutside;

            this.chatPanelPresenter.PointerEntered += HandlePointerEntered;
            this.chatPanelPresenter.PointerExited += HandlePointerExited;
        }

        public void Dispose()
        {
            chatClickDetectionService.OnClickInside -= HandleClickInside;
            chatClickDetectionService.OnClickOutside -= HandleClickOutside;

            chatPanelPresenter.PointerEntered -= HandlePointerEntered;
            chatPanelPresenter.PointerExited -= HandlePointerExited;

            fsm.OnStateChanged -= PropagateStateChange;

            scope.Dispose();
        }

        private void PropagateStateChange() =>
            eventBus.Publish(new ChatEvents.ChatStateChangedEvent
            {
                CurrentState = fsm.CurrentState
            });

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
            {
                // fsm.ChangeState<MinimizedChatState>();
                fsm.PopState();
            }
        }

        public void SetToggleState()
        {
            if (IsMinimized)
                fsm.ChangeState<FocusedChatState>();
            else
                fsm.ChangeState<MinimizedChatState>();
        }

        public void PopState()
        {
            fsm.PopState();
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

        public void SetFocusState()
        {
            fsm.ChangeState<FocusedChatState>();
        }
    }
}
