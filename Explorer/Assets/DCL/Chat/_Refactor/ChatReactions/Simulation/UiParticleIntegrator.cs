using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Integrates screen-space velocity, applies acceleration and drag, advances age, marks expired particles dead.
    /// </summary>
    public static class UiParticleIntegrator
    {
        public static void Step(ChatReactionsUiParticle[] buffer, int count, Vector2 accel, float drag, float dt)
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

                p.screenVel += accel * dt;
                p.screenVel *= Mathf.Exp(-drag * dt);
                p.screenPos += p.screenVel * dt;
            }
        }
    }
}
