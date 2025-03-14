using System;
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
        public readonly ulong TotalAllocatedMemory;
        public readonly int ChunkSize;
        public readonly int ChunksCount;
        public readonly int ChunksInUseCount;
        public readonly ulong ReturnedTimes;
        public readonly ulong AllocatedTimes;

        public SlabAllocatorInfo(
            ulong totalAllocatedMemory,
            int chunkSize,
            int chunksCount,
            int chunksInUseCount,
            ulong returnedTimes,
            ulong allocatedTimes
        )
        {
            TotalAllocatedMemory = totalAllocatedMemory;
            ChunkSize = chunkSize;
            ChunksCount = chunksCount;
            ChunksInUseCount = chunksInUseCount;
            ReturnedTimes = returnedTimes;
            AllocatedTimes = allocatedTimes;
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
        private NativeHashSet<int> freeIndexes;
        private bool disposed;

        private ulong returnedTimes;
        private ulong allocatedTimes;

        public SlabAllocator(int chunkSize, int chunksCount) : this()
        {
            ptr = NativeAlloc.Malloc((nuint)(chunkSize * chunksCount));

            this.chunkSize = chunkSize;
            this.chunksCount = chunksCount;

            freeIndexes = new NativeHashSet<int>(chunksCount, Allocator.Persistent);

            for (int i = 0; i < chunksCount; i++)
                freeIndexes.Add(i);

            returnedTimes = 0;
            allocatedTimes = 0;
        }

        public readonly SlabAllocatorInfo Info => new (((ulong)chunkSize) * (ulong)chunksCount, chunkSize, chunksCount, chunksCount - freeIndexes.Count, returnedTimes, allocatedTimes);

        public bool CanAllocate => freeIndexes.Count > 0;

        public SlabItem Allocate()
        {
            if (CanAllocate == false)
                throw new Exception($"{nameof(SlabAllocator)} on {ptr.ToInt64()} cannot allocate, check {nameof(CanAllocate)} before use");

            int index = freeIndexes.FirstItem();
            freeIndexes.Remove(index);
            allocatedTimes++;
            return new SlabItem(ptr, index, chunkSize);
        }

        public void Release(SlabItem item)
        {
#if UNITY_EDITOR
            if (chunksCount == freeIndexes.Count)
                throw new Exception("Slab is already free");

            if (freeIndexes.Contains(item.index))
                throw new Exception($"Index {item.index} is already freed");
#endif
            freeIndexes.Add(item.index);
            returnedTimes++;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            NativeAlloc.Free(ptr);

            freeIndexes.Dispose();
            disposed = true;
        }
    }

    public struct DynamicSlabAllocator : ISlabAllocator
    {
        private readonly int chunkSize;
        private readonly int chunksCount;
        private NativeList<SlabAllocator> allocators;
        private NativeHashSet<int> freeAllocatorsLookUp;
        private NativeHashMap<IntPtr, int> ptrToAllocatorIndex;

        public DynamicSlabAllocator(int chunkSize, int chunksCount) : this()
        {
            this.chunkSize = chunkSize;
            this.chunksCount = chunksCount;
            allocators = new NativeList<SlabAllocator>(Allocator.Persistent);
            freeAllocatorsLookUp = new NativeHashSet<int>(8, Allocator.Persistent);
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
            freeAllocatorsLookUp.Dispose();
            ptrToAllocatorIndex.Dispose();
        }

        private void AddNewAllocator()
        {
            var allocator = new SlabAllocator(chunkSize, chunksCount);

            int index = allocators.Length;

            allocators.Add(allocator);
            freeAllocatorsLookUp.Add(index);
            ptrToAllocatorIndex.Add(allocator.ptr, index);
        }

        public SlabAllocatorInfo Info
        {
            get
            {
                int count = allocators.Length;
                ulong totalAllocatedMemory = 0;
                int chunksInUseCount = 0;
                ulong totalReturnedTimes = 0;
                ulong totalAllocatedTimes = 0;

                for (int i = 0; i < count; i++)
                {
                    ref SlabAllocator allocator = ref AllocatorAt(i);
                    var info = allocator.Info;
                    totalAllocatedMemory += info.TotalAllocatedMemory;
                    chunksInUseCount += info.ChunksInUseCount;
                    totalReturnedTimes += info.ReturnedTimes;
                    totalAllocatedTimes += info.AllocatedTimes;
                }

                return new SlabAllocatorInfo(totalAllocatedMemory, chunkSize, chunksCount * count, chunksInUseCount, totalReturnedTimes, totalAllocatedTimes);
            }
        }

        public bool CanAllocate => true;

        public SlabItem Allocate()
        {
            int index = NextFreeAllocatorIndex();

            ref SlabAllocator allocator = ref AllocatorAt(index);
            SlabItem item = allocator.Allocate();

            if (allocator.CanAllocate == false)
                freeAllocatorsLookUp.Remove(index);

            return item;
        }

        public void Release(SlabItem item)
        {
            int index = ptrToAllocatorIndex[item.ptr];
            ref SlabAllocator allocator = ref AllocatorAt(index);
            allocator.Release(item);
            freeAllocatorsLookUp.Add(index);
        }

        private int NextFreeAllocatorIndex()
        {
            if (freeAllocatorsLookUp.Count == 0) AddNewAllocator();
            return freeAllocatorsLookUp.FirstItem();
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

    public static class NativeExtensions
    {
        public static T FirstItem<T>(this ref NativeHashSet<T> set) where T: unmanaged, IEquatable<T>
        {
            using var enumerator = set.GetEnumerator();
            enumerator.MoveNext();
            return enumerator.Current;
        }
    }
}
