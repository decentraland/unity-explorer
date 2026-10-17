namespace DCL.Chat.ChatReactions.Simulation.World
{
    /// <summary>
    /// A force applied to world-space particles each simulation tick.
    /// </summary>
    public interface IWorldParticleForce
    {
        void Apply(ChatReactionsParticle[] buffer, int count, float dt);
    }
}
