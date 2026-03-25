using System;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Mutable runtime debug stats for the chat reactions system.
    /// Separated from ChatReactionsConfig to prevent ScriptableObject dirtying every frame.
    /// </summary>
    public class ChatReactionDebugState : IDisposable
    {
        /// <summary>Active instance set at init, cleared on dispose. Used by editor debug window.</summary>
        public static ChatReactionDebugState? Current { get; private set; }

        public ChatReactionStats LastStats { get; private set; }

        public ChatReactionDebugState()
        {
            Current = this;
        }

        public void UpdateStats(ChatReactionStats stats)
        {
            LastStats = stats;
        }

        public void Dispose()
        {
            if (Current == this)
                Current = null;
        }
    }
}
