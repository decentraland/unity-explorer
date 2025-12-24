using DCL.Chat.ChatServices;
using MVC;
using System;
using Utility;

namespace DCL.Chat.ChatStates
{
    public class ChatStateMachine : IDisposable
    {
        private readonly ChatInputBlockingService inputBlocker;
        private readonly ChatClickDetectionHandler chatClickDetectionHandler;
        private readonly IEventBus eventBus;
        private readonly MVCStateMachine fsm;
        private readonly EventSubscriptionScope scope = new ();
        private readonly ChatPanelPresenter chatPanelPresenter;

        private ChatState CurrentChatState => (ChatState)fsm.CurrentState!;

        public bool IsFocused => fsm.CurrentState is FocusedChatState;
        public bool IsMinimized => fsm.CurrentState is MinimizedChatState;
        public bool IsHidden => fsm.CurrentState is HiddenChatState;

        public ChatStateMachine(
            IEventBus eventBus,
            ChatUIMediator mediator,
            ChatInputBlockingService inputBlocker,
            ChatClickDetectionHandler chatClickDetectionHandler,
            ChatPanelPresenter chatPanelPresenter)
        {
            this.inputBlocker = inputBlocker;
            this.chatClickDetectionHandler = chatClickDetectionHandler;
            this.eventBus = eventBus;

            this.chatPanelPresenter = chatPanelPresenter;

            fsm = new MVCStateMachine();

            fsm.AddStates(
                new InitChatState(),
                new DefaultChatState(fsm, mediator),
                new FocusedChatState(fsm, mediator, inputBlocker),
                new MembersChatState(fsm, mediator),
                new MinimizedChatState(fsm, mediator),
                new HiddenChatState(mediator)
            );

            fsm.Enter<InitChatState>();

            fsm.OnStateChanged += PropagateStateChange;

            scope.Add(eventBus.Subscribe<ChatEvents.FocusRequestedEvent>(HandleFocusRequestedEvent));
            scope.Add(eventBus.Subscribe<ChatEvents.CloseChatEvent>(HandleCloseChatEvent));
            scope.Add(eventBus.Subscribe<ChatEvents.ToggleMembersEvent>(HandleToggleMembersEvent));

            chatClickDetectionHandler.OnClickInside += HandleClickInside;
            chatClickDetectionHandler.OnClickOutside += HandleClickOutside;

            this.chatPanelPresenter.PointerEntered += HandlePointerEntered;
            this.chatPanelPresenter.PointerExited += HandlePointerExited;
        }

        public void Dispose()
        {
            chatClickDetectionHandler.OnClickInside -= HandleClickInside;
            chatClickDetectionHandler.OnClickOutside -= HandleClickOutside;

            chatPanelPresenter.PointerEntered -= HandlePointerEntered;
            chatPanelPresenter.PointerExited -= HandlePointerExited;

            fsm.OnStateChanged -= PropagateStateChange;

            scope.Dispose();
        }

        private void PropagateStateChange() =>
            eventBus.Publish(new ChatEvents.ChatStateChangedEvent
            {
                CurrentState = CurrentChatState,
            });

        public void OnViewShow()
        {
            inputBlocker.Initialize();

            fsm.Enter<DefaultChatState>();
        }

        private void HandleFocusRequestedEvent(ChatEvents.FocusRequestedEvent evt)
        {
            CurrentChatState.OnFocusRequested();
        }

        private void HandleCloseChatEvent(ChatEvents.CloseChatEvent evt)
        {
            CurrentChatState.OnCloseRequested();
        }

        private void HandleToggleMembersEvent(ChatEvents.ToggleMembersEvent evt)
        {
            CurrentChatState.OnToggleMembers();
        }

        private void HandleClickInside()
        {
            CurrentChatState.OnClickInside();
        }

        private void HandleClickOutside()
        {
            CurrentChatState.OnClickOutside();
        }

        private void HandlePointerExited()
        {
            CurrentChatState.OnPointerExit();
        }

        private void HandlePointerEntered()
        {
            CurrentChatState.OnPointerEnter();
        }

        public void Minimize()
        {
            CurrentChatState.OnMinimizeRequested();
        }

        public void SetInitialState(bool focus)
        {
            if (focus)
                fsm.Enter<FocusedChatState>();
            else
            {
                // fsm.Enter<MinimizedChatState>();
                fsm.PopState();
            }
        }

        public void SetToggleState()
        {
            if (IsMinimized)
                fsm.Enter<FocusedChatState>();
            else
                fsm.Enter<MinimizedChatState>();
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
                fsm.Enter<DefaultChatState>();
            else
                fsm.Enter<HiddenChatState>();
        }

        public void SetFocusState()
        {
            fsm.Enter<FocusedChatState>();
        }
    }
}
