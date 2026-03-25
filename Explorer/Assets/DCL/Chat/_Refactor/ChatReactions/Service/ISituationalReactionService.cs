namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Union interface for backward compatibility. Prefer the focused interfaces
    /// (ISituationalReactionTrigger, IRemoteReactionTarget, ISituationalReactionSimulation)
    /// in new code.
    /// </summary>
    public interface ISituationalReactionService
        : ISituationalReactionTrigger, IRemoteReactionTarget, ISituationalReactionSimulation
    {
    }
}
