using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Decentraland.Terrain
{
    public static class ParallelWriterExtensions
    {
        public static unsafe bool TryAddNoResize<T>(this NativeList<T>.ParallelWriter writer, T value)
            where T: unmanaged
        {
            int length = Interlocked.Increment(ref writer.ListData -> m_length);

            if (length <= writer.ListData -> Capacity)
            {
                UnsafeUtility.WriteArrayElement(writer.ListData -> Ptr, length - 1, value);
                return true;
            }

            return false;
        }
    }
}
