namespace DCL.Chat.ChatReactions.Networking
{
    /// <summary>
    /// Target for reactions received from remote players via the network bus.
    /// </summary>
    public interface IRemoteReactionTarget
    {
        void HandleRemoteReaction(ReactionReceivedArgs args);
    }
}
