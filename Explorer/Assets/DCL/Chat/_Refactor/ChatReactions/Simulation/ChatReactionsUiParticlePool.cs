using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Fixed-capacity ring-buffer particle pool for screen-space UI particles.
    /// Oldest particles are overwritten when the buffer is full.
    /// </summary>
    public sealed class ChatReactionsUiParticlePool
    {
        private readonly ChatReactionsUiParticle[] particles;
        private int cursor;

        public ChatReactionsUiParticlePool(int capacity)
        {
            particles = new ChatReactionsUiParticle[Mathf.Max(64, capacity)];
        }

        public int Capacity => particles.Length;
        public ChatReactionsUiParticle[] Raw => particles;

        public void Spawn(Vector2 screenPos, Vector2 screenVel, float lifetime, float startSizePx, float endSizePx, int emojiIndex, float zigZagPhase = 0f)
        {
            int i = cursor;
            cursor = (cursor + 1) % particles.Length;

            particles[i] = new ChatReactionsUiParticle
            {
                screenPos = screenPos,
                screenVel = screenVel,
                age = 0f,
                lifetime = lifetime,
                startSizePx = startSizePx,
                endSizePx = endSizePx,
                emojiIndex = emojiIndex,
                zigZagPhase = zigZagPhase,
                alive = 1
            };
        }

        /// <summary>Advances physics for all live particles. Returns alive count.</summary>
        public int Update(float dt, Vector2 accelPx, float drag)
        {
            int aliveCount = 0;

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
                aliveCount++;
            }

            return aliveCount;
        }
    }
}
