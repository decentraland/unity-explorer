using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace DCL.Utilities.Extensions
{
    public static class ArrayExtensions
    {
        public static IReadOnlyList<T> AsReadOnly<T>(this T[] array) =>
            array;

        public static T RandomElement<T>(this IReadOnlyList<T> list) =>
            list[Random.Range(0, list.Count)];

        public static bool EqualsContentInOrder<T>(this T[] a, T[] b)
        {
            if (a.Length != b.Length) return false;

            for (var i = 0; i < a.Length; i++)
                if (!a[i].Equals(b[i]))
                    return false;

            return true;
        }
    }
}
