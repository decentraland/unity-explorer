using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    public struct ChatReactionsParticle
    {
        public Vector3 pos;
        public Vector3 vel;
        public float age;
        public float lifetime;
        public float startSize;
        public float endSize;
        public int emojiIndex;
        public byte alive;
    }

    /// <summary>
    /// Fixed-capacity ring-buffer particle pool. Oldest particles are overwritten
    /// when the buffer is full. Advances physics each frame.
    /// </summary>
    public sealed class ChatReactionsParticlePool
    {
        private readonly ChatReactionsParticle[] particles;
        private int cursor;

        public int Capacity => particles.Length;

        /// <summary>Direct array access for the renderer — do not cache across frames.</summary>
        public ChatReactionsParticle[] Raw => particles;

        public ChatReactionsParticlePool(int capacity)
        {
            particles = new ChatReactionsParticle[Mathf.Max(64, capacity)];
        }

        public void Spawn(Vector3 pos, Vector3 vel, float lifetime, float startSize, float endSize, int emojiIndex)
        {
            int i = cursor;
            cursor = (cursor + 1) % particles.Length;

            particles[i] = new ChatReactionsParticle
            {
                pos = pos,
                vel = vel,
                age = 0f,
                lifetime = lifetime,
                startSize = startSize,
                endSize = endSize,
                emojiIndex = emojiIndex,
                alive = 1,
            };
        }

        /// <summary>Advances physics for all live particles. Returns alive count.</summary>
        public int Tick(float dt, Vector3 gravity, float drag)
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

                p.vel += gravity * dt;
                p.vel *= Mathf.Exp(-drag * dt);
                p.pos += p.vel * dt;
                aliveCount++;
            }

            return aliveCount;
        }
    }
}
