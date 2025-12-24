using DCL.Chat.ChatServices;
using MVC;

namespace DCL.Chat.ChatStates
{
    public class FocusedChatState : ChatState
    {
        private readonly MVCStateMachine stateMachine;
        private readonly ChatUIMediator mediator;
        private readonly ChatInputBlockingService inputBlocker;

        public FocusedChatState(MVCStateMachine stateMachine, ChatUIMediator mediator, ChatInputBlockingService inputBlocker)
        {
            this.stateMachine = stateMachine;
            this.mediator = mediator;
            this.inputBlocker = inputBlocker;
        }

        public override void Enter()
        {
            mediator.SetupForFocusedState();

            inputBlocker.Block();
        }

        public override void Exit()
        {
            inputBlocker.Unblock();
        }

        public override void OnClickOutside() =>
            stateMachine.Enter<DefaultChatState>();

        public override void OnCloseRequested() =>
            stateMachine.Enter<MinimizedChatState>();

        public override void OnMinimizeRequested() =>
            stateMachine.Enter<MinimizedChatState>();

        public override void OnToggleMembers() =>
            stateMachine.Enter<MembersChatState>();
    }
}
