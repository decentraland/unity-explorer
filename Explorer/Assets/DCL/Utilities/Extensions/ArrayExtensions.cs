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
    }
}
