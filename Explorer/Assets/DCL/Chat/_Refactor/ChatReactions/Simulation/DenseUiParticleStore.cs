using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Dense array storage for screen-space UI particles.
    /// <c>Buffer[0..Count-1]</c> are all alive — no gaps.
    /// </summary>
    public sealed class DenseUiParticleStore
    {
        private readonly ChatReactionsUiParticle[] buffer;
        private int count;

        public ChatReactionsUiParticle[] Buffer => buffer;
        public int Count => count;
        public int Capacity => buffer.Length;

        public DenseUiParticleStore(int capacity)
        {
            buffer = new ChatReactionsUiParticle[Mathf.Max(64, capacity)];
        }

        public void Add(ChatReactionsUiParticle particle)
        {
            if (count >= buffer.Length)
            {
                buffer[0] = particle;
                return;
            }

            buffer[count++] = particle;
        }

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
