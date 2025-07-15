using DCL.Chat.ChatMediator;
using DCL.Chat.ChatStates;
using DCL.Chat.EventBus;
using Prime31.StateKit;
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
        private readonly SKStateMachine<ChatStateMachine> fsm;
        private readonly EventSubscriptionScope scope = new ();
        
        public ChatMainController MainController { get; }
        public ChatUIMediator Mediator => mediator;
        public ChatInputBlockingService InputBlocker => inputBlocker;
        public bool IsInputFocused => fsm.currentState is FocusedChatState;
        public bool IsMinimized => fsm.currentState is MinimizedChatState;
        public bool IsHidden => fsm.currentState is HiddenChatState;

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
            
            fsm = new SKStateMachine<ChatStateMachine>(this, new InitChatState());
            fsm.addState(new DefaultChatState());
            fsm.addState(new FocusedChatState());
            fsm.addState(new MembersChatState());
            fsm.addState(new MinimizedChatState());
            fsm.addState(new HiddenChatState());

            scope.Add(eventBus.Subscribe<ChatEvents.FocusRequestedEvent>(HandleFocusRequestedEvent));
            scope.Add(eventBus.Subscribe<ChatEvents.CloseChatEvent>(HandleCloseChatEvent));
            scope.Add(eventBus.Subscribe<ChatEvents.ToggleMembersEvent>(HandleToggleMembersEvent));

            chatClickDetectionService.OnClickInside += HandleClickInside;
            chatClickDetectionService.OnClickOutside += HandleClickOutside;
        }

        public void OnViewShow()
        {
            fsm.changeState<DefaultChatState>();
        }
        
        private void HandleFocusRequestedEvent(ChatEvents.FocusRequestedEvent evt)
        {
            if (fsm.currentState is IFocusRequestHandler handler)
                handler.OnFocusRequested();
        }
        
        private void HandleCloseChatEvent(ChatEvents.CloseChatEvent evt)
        {
            if (fsm.currentState is ICloseRequestHandler handler)
                handler.OnCloseRequested();
        }

        private void HandleToggleMembersEvent(ChatEvents.ToggleMembersEvent evt)
        {
            if (fsm.currentState is IToggleMembersHandler handler)
                handler.OnToggleMembers(evt.IsVisible);
        }
        
        private void HandleClickInside()
        {
            if (fsm.currentState is IClickInsideHandler handler)
                handler.OnClickInside();
        }

        private void HandleClickOutside()
        {
            if (fsm.currentState is IClickOutsideHandler handler)
                handler.OnClickOutside();
        }
        
        public void SetInitialState(bool focus)
        {
            if (focus)
                fsm.changeState<FocusedChatState>();
            else
                fsm.changeState<DefaultChatState>();
        }

        public void Minimize()
        {
            if (fsm.currentState is IMinimizeRequestHandler handler)
                handler.OnMinimizeRequested();
        }
        /// <summary>
        /// NOTE: this method is clunky,
        /// NOTE: but it is used to set the visibility of the chat UI.
        /// </summary>
        /// <param name="isVisible"></param>
        public void SetVisibility(bool isVisible)
        {
            if (isVisible)
            {
                fsm.changeState<DefaultChatState>();
            }
            else
            {
                if (fsm.currentState is not HiddenChatState)
                {
                    fsm.changeState<HiddenChatState>();
                }
            }
        }

        public void Dispose()
        {
            if (chatClickDetectionService != null)
            {
                chatClickDetectionService.OnClickInside -= HandleClickInside;
                chatClickDetectionService.OnClickOutside -= HandleClickOutside;
            }
            
            scope.Dispose();  
        } 
    }
}