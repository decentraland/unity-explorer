namespace DCL.Nametags
{
    /// <summary>
    ///     Keeps the smallest squared distances added since the last <see cref="Clear" /> in a fixed-size max-heap,
    ///     so the largest kept one is the farthest distance that still fits into the cap.
    /// </summary>
    public class NearestTagsCap
    {
        private readonly float[] heap;
        private int count;

        public NearestTagsCap(int capacity)
        {
            heap = new float[capacity];
        }

        /// <summary>
        ///     Tags at this squared distance or closer fit into the cap; infinity while fewer distances than the capacity were added.
        /// </summary>
        public float MaxSqrDistance => count < heap.Length ? float.PositiveInfinity : heap[0];

        public void Clear()
        {
            count = 0;
        }

        public void Add(float sqrDistance)
        {
            if (count < heap.Length)
            {
                SiftUp(count++, sqrDistance);
                return;
            }

            if (sqrDistance < heap[0])
                SiftDown(sqrDistance);
        }

        private void SiftUp(int index, float value)
        {
            while (index > 0)
            {
                int parent = (index - 1) / 2;

                if (heap[parent] >= value)
                    break;

                heap[index] = heap[parent];
                index = parent;
            }

            heap[index] = value;
        }

        private void SiftDown(float value)
        {
            var index = 0;

            while (true)
            {
                int child = (2 * index) + 1;

                if (child >= count)
                    break;

                if (child + 1 < count && heap[child + 1] > heap[child])
                    child++;

                if (heap[child] <= value)
                    break;

                heap[index] = heap[child];
                index = child;
            }

            heap[index] = value;
        }
    }
}
