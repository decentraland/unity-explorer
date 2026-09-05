using System.Collections.Generic;
using UnityEngine;

namespace DCL.Utilities.Extensions
{
    public static class ListExtensions
    {
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;

            while (n > 1)
            {
                int k = Random.Range(0, n);
                n--;
                T tmp = list[k];
                list[k] = list[n];
                list[n] = tmp;
            }
        }
    }
}
