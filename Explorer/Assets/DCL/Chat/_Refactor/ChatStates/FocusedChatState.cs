using DCL.Chat.ChatServices;
using MVC;
using UnityEngine;

namespace DCL.Chat.ChatStates
{
    public class FocusedChatState : ChatState, IState
    {
        private readonly MVCStateMachine<ChatState> stateMachine;
        private readonly ChatUIMediator mediator;
        private readonly ChatInputBlockingService inputBlocker;

        public FocusedChatState(MVCStateMachine<ChatState> stateMachine, ChatUIMediator mediator, ChatInputBlockingService inputBlocker)
        {
            this.stateMachine = stateMachine;
            this.mediator = mediator;
            this.inputBlocker = inputBlocker;
        }

        public void Enter()
        {
            Debug.Log("[PACO] >>> FocusedChatState.Enter()");
            mediator.SetupForFocusedState();

            inputBlocker.Block();
        }

        public override void Exit()
        {
            Debug.Log($"[PACO] <<< FocusedChatState.Exit()\n{System.Environment.StackTrace}");
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
