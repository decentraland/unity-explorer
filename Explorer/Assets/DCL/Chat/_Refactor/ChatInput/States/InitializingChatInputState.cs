namespace DCL.Chat.ChatInput
{
    /// <summary>
    ///     Denotes the state when the capability to send messages is being resolved
    /// </summary>
    public class InitializingChatInputState : ChatInputState
    {
        protected override void OnInputUnblocked()
        {
            machine.Enter<TypingEnabledChatInputState>();
        }

        protected override void OnInputBlocked()
        {
            machine.Enter<BlockedChatInputState>();
        }
    }
}
