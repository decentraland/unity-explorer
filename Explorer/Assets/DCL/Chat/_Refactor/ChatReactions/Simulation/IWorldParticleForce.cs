namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// A force that can be applied to world-space particles each frame.
    /// Forces are composed into a pipeline and executed in order.
    /// </summary>
    public interface IWorldParticleForce
    {
        void Apply(ChatReactionsParticle[] buffer, int count, float dt);
    }
}
