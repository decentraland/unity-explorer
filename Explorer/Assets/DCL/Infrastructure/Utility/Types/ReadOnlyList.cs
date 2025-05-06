using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DCL.Infrastructure.Utility.Types
{
    /// <summary>
    /// Wrap over List to avoid virtual calls on casting to IReadOnlyList interface to improve performance
    /// </summary>
    public readonly struct ReadOnlyList<T> : IReadOnlyList<T>
    {
        private readonly List<T> list;

        internal ReadOnlyList(List<T> list) : this()
        {
            this.list = list;
        }

        public IEnumerator<T> GetEnumerator() =>
            list.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();

        public int Count => list.Count;

        public T this[int index] => list[index];
    }

    public static class ReadOnlyListExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyList<T> AsReadOnlyList<T>(this List<T> list) =>
            new (list);
    }
}
