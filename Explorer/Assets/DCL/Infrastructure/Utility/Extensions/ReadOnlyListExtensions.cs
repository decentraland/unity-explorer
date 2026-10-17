using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace DCL.Utilities.Extensions
{
    public static class ReadOnlyListExtensions
    {
        public static void Shuffle<T>(this IReadOnlyList<T> list, List<T> destination)
        {
            int n = list.Count;

            destination.AddRange(list);

            while (n > 1)
            {
                int k = Random.Range(0, n);
                n--;
                T tmp = destination[k];
                destination[k] = destination[n];
                destination[n] = tmp;
            }
        }

        public static int IndexOf<T>(this IReadOnlyList<T> collection, T item)
        {
            for (int i = 0; i < collection.Count; i++)
                if (EqualityComparer<T>.Default.Equals(collection[i], item)) return i;

            return -1;
        }
    }
}
