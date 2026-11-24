namespace DCL.Chat.ChatReactions.Networking
{
    /// <summary>
    ///     Hands the <see cref="ChatMessageReactionService" /> to code that is built before the chat plugin
    ///     creates it (the MCP tools). ChatContainer owns the slot; ChatPlugin fills it once the reactions
    ///     feature exists and empties it when it is disposed.
    /// </summary>
    public sealed class MessageReactionServiceSlot
    {
        /// <summary>
        ///     The service, or null until the chat plugin has finished loading.
        /// </summary>
        public ChatMessageReactionService? Service { get; private set; }

        public void Attach(ChatMessageReactionService service) =>
            Service = service;

        public void Detach() =>
            Service = null;
    }
}
