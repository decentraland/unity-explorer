using System.Collections.Generic;

namespace DCL.Billboard.Extensions
{
    public static class ArrayExtensions
    {
        public static IReadOnlyList<T> AsReadOnly<T>(this T[] array) =>
            array;
    }
}
