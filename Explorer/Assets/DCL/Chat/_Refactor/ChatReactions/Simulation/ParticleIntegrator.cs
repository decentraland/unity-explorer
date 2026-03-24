using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Integrates velocity, applies gravity and drag, advances age, and marks expired particles dead.
    /// Operates on world-space particles. Does not compact — call <see cref="DenseParticleStore.CompactDead"/> after.
    /// </summary>
    public static class ParticleIntegrator
    {
        public static void Step(ChatReactionsParticle[] buffer, int count, Vector3 gravity, float drag, float dt)
        {
            for (int i = 0; i < count; i++)
            {
                ref var p = ref buffer[i];

                p.age += dt;

                if (p.age >= p.lifetime)
                {
                    p.alive = 0;
                    continue;
                }

                p.vel += gravity * dt;
                p.vel *= Mathf.Exp(-drag * dt);
                p.pos += p.vel * dt;
            }
        }
    }
}
