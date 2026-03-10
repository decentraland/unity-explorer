using MVC;

namespace DCL.Chat.ChatInput
{
    /// <summary>
    ///     Denotes the state when the capability to send messages is being resolved
    /// </summary>
    public class InitializingChatInputState : ChatInputState, IState
    {
        private readonly MVCStateMachine<ChatInputState> chatInputStateMachine;

        public InitializingChatInputState(MVCStateMachine<ChatInputState> chatInputStateMachine)
        {
            this.chatInputStateMachine = chatInputStateMachine;
        }

        public void Enter() { }

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
