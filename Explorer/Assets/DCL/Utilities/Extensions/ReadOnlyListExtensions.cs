using System.Collections.Generic;
using UnityEngine;

namespace DCL.Utilities.Extensions
{
    public static class ReadOnlyListExtensions
    {
        public static void Shuffle<T>(this IReadOnlyList<T> list, IList<T> destination)
        {
            int n = list.Count;

            while (n > 1)
            {
                int k = Random.Range(0, n);
                n--;
                T value = list[k];
                destination[k] = list[n];
                destination[n] = value;
            }
        }
    }
}
