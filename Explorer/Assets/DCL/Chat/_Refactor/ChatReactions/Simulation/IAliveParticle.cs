namespace DCL.Chat.ChatReactions.Simulation
{
    /// <summary>
    /// Constraint for particles used with <see cref="DenseParticleStore{T}"/>.
    /// Implementations must expose an alive byte: 0 = dead, non-zero = alive.
    /// IMPORTANT: The struct constraint on DenseParticleStore&lt;T&gt; is load-bearing —
    /// without it, accessing Alive through the interface would box the struct on every call.
    /// </summary>
    public interface IAliveParticle
    {
        byte Alive { get; }
    }
}
