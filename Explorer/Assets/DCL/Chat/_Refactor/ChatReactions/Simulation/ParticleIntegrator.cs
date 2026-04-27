using DCL.Chat.ChatReactions.Simulation.UI;
using DCL.Chat.ChatReactions.Simulation.World;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Simulation
{
    /// <summary>
    /// Integrates velocity, applies acceleration and drag, advances age, and marks expired particles dead.
    /// Does not compact — call <see cref="DenseParticleStore{T}.CompactDead"/> after.
    /// </summary>
    public static class ParticleIntegrator
    {
        public static void Step(ChatReactionsParticle[] buffer, int count, Vector3 gravity, float drag, float dt)
        {
            float dragFactor = Mathf.Exp(-drag * dt);

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
                p.vel *= dragFactor;
                p.pos += p.vel * dt;
            }
        }

        public static void Step(ChatReactionsUiParticle[] buffer, int count, Vector2 accel, float drag, float dt)
        {
            float dragFactor = Mathf.Exp(-drag * dt);

            for (int i = 0; i < count; i++)
            {
                ref var p = ref buffer[i];

                p.age += dt;

                if (p.age >= p.lifetime)
                {
                    p.alive = 0;
                    continue;
                }

                p.screenVel += accel * dt;
                p.screenVel *= dragFactor;
                p.screenPos += p.screenVel * dt;
            }
        }
    }
}
