using System;
using System.Buffers;
using DCL.Optimization.ThreadSafePool;

namespace Utility.Memory
{
    public unsafe class UnmanagedMemoryManager<T> : MemoryManager<T> where T: unmanaged
    {
        private static readonly ThreadSafeObjectPool<UnmanagedMemoryManager<T>> POOL = new (() => new UnmanagedMemoryManager<T>());

        private void* ptr;
        private int length;

        public static UnmanagedMemoryManager<T> New(void* ptr, int length)
        {
            var instance = POOL.Get();
            instance.ptr = ptr;
            instance.length = length;
            return instance;
        }

        public static void Release(UnmanagedMemoryManager<T> instance)
        {
            POOL.Release(instance);
        }

        public override Span<T> GetSpan() =>
            new (ptr, length);

        public override MemoryHandle Pin(int elementIndex = 0) =>
            new ((byte*)ptr + (elementIndex * sizeof(T)));

        public override void Unpin() { }

        protected override void Dispose(bool disposing) { }
    }
}
