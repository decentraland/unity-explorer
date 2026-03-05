using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    public struct UiReactionParticle
    {
        public Vector2 screenPos;
        public Vector2 screenVel;
        public float age;
        public float lifetime;
        public float startSizePx;
        public float endSizePx;
        public int emojiIndex;
        public byte alive;
    }

    public sealed class UiReactionParticlePool
    {
        private readonly UiReactionParticle[] particles;
        private int cursor;

        public UiReactionParticlePool(int capacity)
        {
            particles = new UiReactionParticle[Mathf.Max(64, capacity)];
        }

        public UiReactionParticle[] Raw => particles;

        public void Spawn(Vector2 screenPos, Vector2 screenVel, float lifetime, float startSizePx, float endSizePx, int emojiIndex)
        {
            int i = cursor;
            cursor = (cursor + 1) % particles.Length;

            particles[i] = new UiReactionParticle
            {
                screenPos = screenPos,
                screenVel = screenVel,
                age = 0f,
                lifetime = lifetime,
                startSizePx = startSizePx,
                endSizePx = endSizePx,
                emojiIndex = emojiIndex,
                alive = 1
            };
        }

        public void Update(float dt, Vector2 accelPx, float drag)
        {
            for (int i = 0; i < particles.Length; i++)
            {
                ref var p = ref particles[i];
                if (p.alive == 0) continue;

                p.age += dt;

                if (p.age >= p.lifetime)
                {
                    p.alive = 0;
                    continue;
                }

                p.screenVel += accelPx * dt;
                p.screenVel *= Mathf.Exp(-drag * dt);
                p.screenPos += p.screenVel * dt;
            }
        }
    }
}
