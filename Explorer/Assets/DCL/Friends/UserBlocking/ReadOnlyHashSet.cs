using System;
using System.Collections.Generic;

namespace DCL.Friends.UserBlocking
{
    public class ReadOnlyHashSet<T> : IReadOnlyCollection<T>
    {
        private readonly HashSet<T> set;

        public ReadOnlyHashSet(HashSet<T> set)
        {
            this.set = set ?? throw new ArgumentNullException(nameof(set));
        }

        public bool Contains(T item) => set.Contains(item);

        public int Count => set.Count;

        public IEnumerator<T> GetEnumerator() => set.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
