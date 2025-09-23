using System.Collections.Generic;
using UnityEngine;

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
    }
}
