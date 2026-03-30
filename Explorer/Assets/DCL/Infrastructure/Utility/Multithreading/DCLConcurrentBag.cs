using System.Collections;
using System.Collections.Generic;

namespace Utility.Multithreading
{
    /// <summary>
    ///     WebGL-compatible drop-in replacement for <see cref="System.Collections.Concurrent.ConcurrentBag{T}" />.
    ///     On non-WebGL platforms the real <c>ConcurrentBag</c> is used for thread-safe access.
    ///     On WebGL (single-threaded) a plain <see cref="System.Collections.Generic.List{T}" /> is used instead,
    ///     since WebGL has no OS threads and the locking overhead of <c>ConcurrentBag</c> is unnecessary.
    /// </summary>
    public class DCLConcurrentBag<TValue> : IEnumerable<TValue>
    {
#if !UNITY_WEBGL
        private readonly System.Collections.Concurrent.ConcurrentBag<TValue> Inner;
#else
        private readonly List<TValue> Inner;
#endif

        public DCLConcurrentBag()
        {
            Inner = new();
        }

        public DCLConcurrentBag(IEnumerable<TValue> items)
        {
            Inner = new(items);
        }

        public int Count => Inner.Count;

        public void Clear()
        {
            Inner.Clear();
        }

        public void Add(TValue item)
        {
            Inner.Add(item);
        }

        public bool TryTake(out TValue value)
        {
#if !UNITY_WEBGL
            return Inner.TryTake(out value);
#else
            if (Inner.Count > 0)
            {
                int last = Inner.Count - 1;
                value = Inner[last];
                Inner.RemoveAt(last);
                return true;
            }

            value = default;
            return false;
#endif
        }

        public bool TryPeek(out TValue value)
        {
#if !UNITY_WEBGL
            return Inner.TryPeek(out value);
#else
            if (Inner.Count > 0)
            {
                value = Inner[Inner.Count - 1];
                return true;
            }

            value = default;
            return false;
#endif
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            return Inner.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Inner.GetEnumerator();
        }
    }
}
