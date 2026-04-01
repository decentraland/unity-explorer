using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Dense array storage for particles.
    /// <c>Buffer[0..Count-1]</c> are all alive — no gaps, no alive checks needed in loops.
    /// </summary>
    public sealed class DenseParticleStore<T> where T : struct, IAliveParticle
    {
        private readonly T[] buffer;
        private int count;
        private int overwriteCursor;

        public T[] Buffer => buffer;
        public int Count => count;
        public int Capacity => buffer.Length;

        public DenseParticleStore(int capacity)
        {
            buffer = new T[Mathf.Max(64, capacity)];
        }

        /// <summary>
        /// Attempts to add a particle. Returns false if the store is at capacity.
        /// </summary>
        public bool TryAdd(T particle)
        {
            if (count >= buffer.Length)
                return false;

            buffer[count++] = particle;
            return true;
        }

        /// <summary>
        /// Appends a particle. When full, evicts via round-robin cursor.
        /// Prefer <see cref="TryAdd"/> when dropping is acceptable.
        /// </summary>
        public void ForceAdd(T particle)
        {
            if (count >= buffer.Length)
            {
                buffer[overwriteCursor] = particle;
                overwriteCursor = (overwriteCursor + 1) % buffer.Length;
                return;
            }

            buffer[count++] = particle;
        }

        /// <summary>
        /// Removes dead particles (Alive == 0) by shifting survivors forward.
        /// Single forward pass, stable order, updates <see cref="Count"/>.
        /// </summary>
        public void CompactDead()
        {
            int writeIdx = 0;

            for (int i = 0; i < count; i++)
            {
                if (buffer[i].Alive == 0)
                    continue;

                if (writeIdx != i)
                    buffer[writeIdx] = buffer[i];

                writeIdx++;
            }

            count = writeIdx;
            overwriteCursor = 0;
        }
    }
}
