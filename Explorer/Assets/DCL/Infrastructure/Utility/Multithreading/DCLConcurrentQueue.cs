using System.Collections;
using System.Collections.Generic;

namespace Utility.Multithreading
{
    // WebGL friendly implementation of concurrent queue
    public class DCLConcurrentQueue<T> : IReadOnlyCollection<T>
    {
#if !UNITY_WEBGL
        private readonly System.Collections.Concurrent.ConcurrentQueue<T> Inner;
#else
        private readonly Queue<T> Inner;
#endif

        public DCLConcurrentQueue()
        {
            Inner = new();
        }

        public DCLConcurrentQueue(IEnumerable<T> items)
        {
            Inner = new(items);
        }

        public int Count => Inner.Count;

#if !UNITY_WEBGL
        public bool IsEmpty => Inner.IsEmpty;
#else
        public bool IsEmpty => Inner.Count == 0;
#endif

        public void Enqueue(T item)
        {
            Inner.Enqueue(item);
        }

        public bool TryDequeue(out T value)
        {
#if !UNITY_WEBGL
            return Inner.TryDequeue(out value);
#else
            if (Inner.Count > 0)
            {
                value = Inner.Dequeue();
                return true;
            }

            value = default;
            return false;
#endif
        }

        public bool TryPeek(out T value)
        {
#if !UNITY_WEBGL
            return Inner.TryPeek(out value);
#else
            if (Inner.Count > 0)
            {
                value = Inner.Peek();
                return true;
            }

            value = default;
            return false;
#endif
        }

        public void Clear()
        {
            Inner.Clear();
        }

        public void CopyTo(T[] array, int index)
        {
            Inner.CopyTo(array, index);
        }

        public T[] ToArray() =>
            Inner.ToArray();

        public IEnumerator<T> GetEnumerator() =>
            Inner.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            Inner.GetEnumerator();
    }
}
