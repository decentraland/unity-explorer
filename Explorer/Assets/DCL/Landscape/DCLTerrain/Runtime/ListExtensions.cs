using System.Collections.Generic;

namespace Decentraland.Terrain
{
    public static class ListExtensions
    {
        public static void EnsureCapacity<T>(this List<T> list, int capacity)
        {
            if (list.Capacity < capacity)
                list.Capacity = capacity;
        }
    }
}
