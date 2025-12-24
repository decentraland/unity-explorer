using DCL.Chat.ChatServices;
using System;

namespace DCL.Chat.ChatStates
{
    public class FocusedChatState : ChatState
    {
        private readonly ChatUIMediator mediator;
        private readonly ChatInputBlockingService inputBlocker;

        public FocusedChatState(ChatUIMediator mediator, ChatInputBlockingService inputBlocker)
        {
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
            machine.Enter<DefaultChatState>();

        public override void OnCloseRequested() =>
            machine.Enter<MinimizedChatState>();

        public override void OnMinimizeRequested() =>
            machine.Enter<MinimizedChatState>();

        public override void OnToggleMembers() =>
            machine.Enter<MembersChatState>();
    }
}
