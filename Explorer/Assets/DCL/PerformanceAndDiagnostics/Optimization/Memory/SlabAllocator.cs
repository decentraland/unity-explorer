using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace DCL.Optimization.Memory
{
    public readonly struct SlabItem
    {
        internal readonly IntPtr ptr;
        internal readonly int index;
        public readonly int chunkSize;
        private readonly int offset;

        public SlabItem(IntPtr ptr, int index, int chunkSize)
        {
            this.ptr = ptr;
            this.index = index;
            this.chunkSize = chunkSize;
            offset = chunkSize * index;
        }

        public Span<byte> AsSpan()
        {
            unsafe
            {
                byte* p = (byte*)ptr.ToPointer();
                p += offset;
                return new Span<byte>(p, chunkSize);
            }
        }
    }

    public readonly struct SlabAllocatorInfo
    {
        public readonly int TotalAllocatedMemory;
        public readonly int ChunkSize;
        public readonly int ChunksCount;
        public readonly int ChunksInUseCount;

        public SlabAllocatorInfo(int totalAllocatedMemory, int chunkSize, int chunksCount, int chunksInUseCount)
        {
            TotalAllocatedMemory = totalAllocatedMemory;
            ChunkSize = chunkSize;
            ChunksCount = chunksCount;
            ChunksInUseCount = chunksInUseCount;
        }
    }

    public interface ISlabAllocator : IDisposable
    {
        /// <summary>
        /// Shared slab allocator with size 16 MB per slab
        /// </summary>
        public static readonly ThreadSafeSlabAllocator<DynamicSlabAllocator> SHARED = new (new DynamicSlabAllocator(128 * 1024, 128));

        SlabAllocatorInfo Info { get; }

        bool CanAllocate { get; }

        SlabItem Allocate();

        void Release(SlabItem item);
    }

    public struct SlabAllocator : ISlabAllocator
    {
        internal readonly IntPtr ptr;
        private readonly int chunkSize;
        private readonly int chunksCount;
        private NativeArray<int> freeIndexes;
        private int freeCount;
        private bool disposed;

        public SlabAllocator(int chunkSize, int chunksCount) : this()
        {
            ptr = Malloc((nuint) (chunkSize * chunksCount));

            this.chunkSize = chunkSize;
            this.chunksCount = chunksCount;

            freeIndexes = new NativeArray<int>(chunksCount, Allocator.Persistent);
            freeCount = chunksCount;

            for (int i = 0; i < freeCount; i++)
                freeIndexes[i] = i;
        }

        public readonly SlabAllocatorInfo Info => new (chunkSize * chunksCount, chunkSize, chunksCount, chunksCount - freeCount);

        public bool CanAllocate => freeCount > 0;

        public bool FullFree => freeCount == chunksCount;

        public SlabItem Allocate()
        {
            if (CanAllocate == false)
                throw new Exception($"{nameof(SlabAllocator)} on {ptr.ToInt64()} cannot allocate, check {nameof(CanAllocate)} before use");

            freeCount--;
            int freeChunkIndex = freeIndexes[freeCount];
            return new SlabItem(ptr, freeChunkIndex, chunkSize);
        }

        public void Release(SlabItem item)
        {
            freeIndexes[freeCount] = item.index;
            freeCount++;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            Free(ptr);

            freeIndexes.Dispose();
            disposed = true;
        }

        [DllImport("libc", EntryPoint = "malloc", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr Malloc(nuint size);

        [DllImport("libc", EntryPoint = "free", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Free(IntPtr ptr);
    }

    public struct DynamicSlabAllocator : ISlabAllocator
    {
        private readonly int chunkSize;
        private readonly int chunksCount;
        private NativeList<SlabAllocator> allocators;
        private NativeList<int> freeAllocators;
        private NativeHashMap<IntPtr, int> ptrToAllocatorIndex;

        public DynamicSlabAllocator(int chunkSize, int chunksCount) : this()
        {
            this.chunkSize = chunkSize;
            this.chunksCount = chunksCount;
            allocators = new NativeList<SlabAllocator>(Allocator.Persistent);
            freeAllocators = new NativeList<int>(8, Allocator.Persistent);
            ptrToAllocatorIndex = new NativeHashMap<IntPtr, int>(8, Allocator.Persistent);

            AddNewAllocator();
        }

        public void Dispose()
        {
            for (int i = 0; i < allocators.Length; i++)
            {
                ref SlabAllocator allocator = ref AllocatorAt(i);
                allocator.Dispose();
            }

            allocators.Dispose();
            freeAllocators.Dispose();
            ptrToAllocatorIndex.Dispose();
        }

        private void AddNewAllocator()
        {
            var allocator = new SlabAllocator(chunkSize, chunksCount);

            int index = allocators.Length;

            allocators.Add(allocator);
            freeAllocators.Add(index);
            ptrToAllocatorIndex.Add(allocator.ptr, index);
        }

        public SlabAllocatorInfo Info
        {
            get
            {
                int count = allocators.Length;
                int totalAllocatedMemory = 0;
                int chunksInUseCount = 0;

                for (int i = 0; i < count; i++)
                {
                    ref SlabAllocator allocator = ref AllocatorAt(i);
                    var info = allocator.Info;
                    totalAllocatedMemory += info.TotalAllocatedMemory;
                    chunksInUseCount += info.ChunksInUseCount;
                }

                return new SlabAllocatorInfo(totalAllocatedMemory, chunkSize, chunksCount * count, chunksInUseCount);
            }
        }

        public bool CanAllocate => true;

        public SlabItem Allocate()
        {
            if (freeAllocators.Length == 0)
                AddNewAllocator();

            int index = freeAllocators.Length - 1;
            int allocatorIndex = freeAllocators[index];

            ref SlabAllocator allocator = ref AllocatorAt(allocatorIndex);
            SlabItem item = allocator.Allocate();

            if (allocator.CanAllocate == false)
                freeAllocators.RemoveAt(index);

            return item;
        }

        public void Release(SlabItem item)
        {
            int index = ptrToAllocatorIndex[item.ptr];
            ref SlabAllocator allocator = ref AllocatorAt(index);

            if (allocator.CanAllocate == false)
                freeAllocators.Add(index);

            allocator.Release(item);
        }

        private ref SlabAllocator AllocatorAt(int index)
        {
            unsafe
            {
                ref SlabAllocator allocator = ref UnsafeUtility.ArrayElementAsRef<SlabAllocator>(allocators.GetUnsafePtr(), index);
                return ref allocator;
            }
        }
    }

    public class ThreadSafeSlabAllocator<T> : ISlabAllocator where T: ISlabAllocator
    {
        private T allocator;

        public ThreadSafeSlabAllocator(T allocator)
        {
            this.allocator = allocator;
        }

        public void Dispose()
        {
            lock (this) { allocator.Dispose(); }
        }

        public SlabAllocatorInfo Info
        {
            get
            {
                lock (this) { return allocator.Info; }
            }
        }

        public bool CanAllocate
        {
            get
            {
                lock (this) { return allocator.CanAllocate; }
            }
        }

        public SlabItem Allocate()
        {
            lock (this) { return allocator.Allocate(); }
        }

        public void Release(SlabItem item)
        {
            lock (this) { allocator.Release(item); }
        }
    }
}
