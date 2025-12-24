using MVC;

namespace DCL.Chat.ChatInput
{
    /// <summary>
    ///     Denotes the state when the capability to send messages is being resolved
    /// </summary>
    public class InitializingChatInputState : ChatInputState
    {
        private readonly MVCStateMachine chatInputStateMachine;

        public InitializingChatInputState(MVCStateMachine chatInputStateMachine)
        {
            this.chatInputStateMachine = chatInputStateMachine;
        }

        protected override void OnInputUnblocked()
        {
            chatInputStateMachine.Enter<TypingEnabledChatInputState>();
        }

        protected override void OnInputBlocked()
        {
            chatInputStateMachine.Enter<BlockedChatInputState>();
        }
    }
}
