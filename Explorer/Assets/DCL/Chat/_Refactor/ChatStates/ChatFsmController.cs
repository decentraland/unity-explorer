using DCL.Chat.ChatMediator;
using DCL.Chat.ChatStates;
using DCL.Chat.EventBus;
using Prime31.StateKit;
using System;

namespace DCL.Chat._Refactor.ChatStates
{
    public class ChatFsmController : IDisposable
    {
        internal readonly IEventBus eventBus;
        private readonly ChatUIMediator mediator;
        private readonly ChatInputBlockingService inputBlocker;
        private readonly ChatClickDetectionService chatClickDetectionService;
        private readonly SKStateMachine<ChatFsmController> fsm;
        private readonly EventSubscriptionScope scope = new ();
        
        public ChatMainController MainController { get; }
        public ChatUIMediator Mediator => mediator;
        public ChatInputBlockingService InputBlocker => inputBlocker;
        public bool IsInputFocused => fsm.currentState is FocusedChatState;
        public bool IsMinimized => fsm.currentState is MinimizedChatState;

        public ChatFsmController(
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
            
            fsm = new SKStateMachine<ChatFsmController>(this, new InitChatState());
            fsm.addState(new DefaultChatState());
            fsm.addState(new FocusedChatState());
            fsm.addState(new MembersChatState());
            fsm.addState(new MinimizedChatState());

            scope.Add(this.eventBus.Subscribe<ChatEvents.FocusRequestedEvent>(evt => fsm.changeState<FocusedChatState>()));
            scope.Add(this.eventBus.Subscribe<ChatEvents.CloseChatEvent>(evt => Minimize()));
            scope.Add(this.eventBus.Subscribe<ChatEvents.ToggleMembersEvent>(evt => OnToggleMembers(evt.IsVisible)));

            chatClickDetectionService.OnClickInside += OnClickInside;
            chatClickDetectionService.OnClickOutside += OnClickOutside;
        }

        public void OnViewShow()
        {
            fsm.changeState<DefaultChatState>();
        }
        
        public void Minimize()
        {
            fsm.changeState<MinimizedChatState>();
        }

        private void OnClickInside()
        {
            if (fsm.currentState is DefaultChatState)
            {
                fsm.changeState<FocusedChatState>();
            }
        }

        private void OnClickOutside()
        {
            if (IsInputFocused)
            {
                fsm.changeState<DefaultChatState>();
            }
        }

        private void OnToggleMembers(bool isVisible)
        {
            if (isVisible) fsm.changeState<MembersChatState>();
            else fsm.changeState<FocusedChatState>();
        }
        
        public void SetInitialState(bool focus)
        {
            if (focus)
                fsm.changeState<FocusedChatState>();
            else
                fsm.changeState<DefaultChatState>();
        }

        public void Dispose()
        {
            if (chatClickDetectionService != null)
            {
                chatClickDetectionService.OnClickInside -= OnClickInside;
                chatClickDetectionService.OnClickOutside -= OnClickOutside;
            }
            
            scope.Dispose();  
        } 
    }
}