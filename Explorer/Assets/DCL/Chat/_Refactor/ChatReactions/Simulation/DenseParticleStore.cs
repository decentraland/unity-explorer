using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Dense array storage for world-space particles.
    /// <c>Buffer[0..Count-1]</c> are all alive — no gaps, no alive checks needed in loops.
    /// </summary>
    public sealed class DenseParticleStore
    {
        private readonly ChatReactionsParticle[] buffer;
        private int count;

        public ChatReactionsParticle[] Buffer => buffer;
        public int Count => count;
        public int Capacity => buffer.Length;

        public DenseParticleStore(int capacity)
        {
            buffer = new ChatReactionsParticle[Mathf.Max(64, capacity)];
        }

        /// <summary>
        /// Appends a particle. When full, overwrites index 0 (oldest after compaction).
        /// </summary>
        public void Add(ChatReactionsParticle particle)
        {
            if (count >= buffer.Length)
            {
                buffer[0] = particle;
                return;
            }

            buffer[count++] = particle;
        }

        /// <summary>
        /// Removes dead particles (alive == 0) by shifting survivors forward.
        /// Single forward pass, stable order, updates <see cref="Count"/>.
        /// </summary>
        public void CompactDead()
        {
            int writeIdx = 0;

            for (int i = 0; i < count; i++)
            {
                if (buffer[i].alive == 0)
                    continue;

                if (writeIdx != i)
                    buffer[writeIdx] = buffer[i];

                writeIdx++;
            }

            count = writeIdx;
        }
    }
}
