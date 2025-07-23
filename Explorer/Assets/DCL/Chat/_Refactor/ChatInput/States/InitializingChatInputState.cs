namespace DCL.Chat
{
    /// <summary>
    ///     Denotes the state when the capability to send messages is being resolved
    /// </summary>
    public class InitializingChatInputState : ChatInputState
    {
        protected override void OnInputUnblocked()
        {
            ChangeState<TypingEnabledChatInputState>();
        }

        protected override void OnInputBlocked()
        {
            ChangeState<BlockedChatInputState>();
        }
    }
}
