namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Mutable runtime debug stats for the chat reactions system.
    /// Separated from ChatReactionsConfig to prevent ScriptableObject dirtying every frame.
    /// </summary>
    public class ChatReactionDebugState
    {
        public ChatReactionStats LastStats { get; private set; }

        public void UpdateStats(ChatReactionStats stats)
        {
            LastStats = stats;
        }
    }
}
