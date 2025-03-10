using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.Optimization.Memory
{
    public class MemoryChain : IDisposable
    {
        public static readonly MemoryChain EMPTY = new (null!);

        private static long instancesCount;
        private static long instancesMemory;

        public static long InstancesCount => instancesCount;
        public static long InstancesMemory => instancesMemory;

        /// <summary>
        /// Doesn't own allocator
        /// </summary>
        private readonly ThreadSafeSlabAllocator<DynamicSlabAllocator> allocator;
        internal readonly List<SlabItem> slabs;
        internal int leftSpaceInLast { get; private set; }

#if UNITY_EDITOR || DEBUG
        private string? disposedBy;
#endif

        public int TotalLength
        {
            get
            {
                {
                    int totalValid = 0;

                    for (int i = 0; i < slabs.Count; i++)
                    {
                        int slabSize = slabs[i].AsSpan().Length;
                        totalValid += IsLastSlab(i) ? slabSize - leftSpaceInLast : slabSize;
                    }

                    return totalValid;
                }
            }
        }

        //TODO pooling
        public MemoryChain(ThreadSafeSlabAllocator<DynamicSlabAllocator> allocator)
        {
            this.allocator = allocator;
            slabs = ListPool<SlabItem>.Get()!;
            leftSpaceInLast = 0;

            if (allocator != null)
                Interlocked.Increment(ref instancesCount);
        }

        public void Dispose()
        {
#if UNITY_EDITOR || DEBUG
            if (disposedBy != null)
                throw new InvalidOperationException($"MemoryChain was already disposed by {disposedBy}");
#endif

            if (allocator == null)
                return;

#if UNITY_EDITOR || DEBUG
            if (disposedBy != null)
                disposedBy = Environment.StackTrace;
#endif
            int total = this.TotalLength;

            for (int i = 0; i < slabs.Count; i++) allocator.Release(slabs[i]);
            slabs.Clear();
            ListPool<SlabItem>.Release(slabs);

            Interlocked.Decrement(ref instancesCount);
            Interlocked.Add(ref instancesMemory, -total);
        }

        public void AppendData(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return;

            if (leftSpaceInLast == 0)
                AllocateNewSlab();

            var dataOffset = 0;

            while (dataOffset < data.Length)
            {
                int copySize = Math.Min(data.Length - dataOffset, leftSpaceInLast);
                Span<byte> span = LastSpan();

                data.Slice(dataOffset, copySize).CopyTo(span.Slice(span.Length - leftSpaceInLast));
                dataOffset += copySize;
                leftSpaceInLast -= copySize;

                Interlocked.Add(ref instancesMemory, copySize);

                if (leftSpaceInLast == 0 && dataOffset < data.Length)
                    AllocateNewSlab();
            }
        }

        public void AppendData(in MemoryChain data)
        {
            for (int i = 0; i < data.slabs.Count - 1; i++) AppendData(data.slabs[i].AsSpan());
            var lastSpan = data.LastSpan();
            AppendData(lastSpan.Slice(0, lastSpan.Length - data.leftSpaceInLast));
        }

        public unsafe void AppendData(void* memory, int size)
        {
            AppendData(new Span<byte>(memory, size));
        }

        /// <summary>
        /// Consumes MemoryChain and returns a stream that reads from it
        /// </summary>
        /// <returns></returns>
        public Stream ToStream() =>
            ChainStream.New(this);

        public ChainMemoryIterator AsMemoryIterator() =>
            new (this);

        private void AllocateNewSlab()
        {
            SlabItem newSlab = allocator.Allocate();
            slabs.Add(newSlab);
            leftSpaceInLast = LastSpan().Length;
        }

        private bool IsLastSlab(int index) =>
            index == slabs.Count - 1;

        private Span<byte> LastSpan()
        {
            int lastSlabIndex = slabs.Count - 1;
            SlabItem targetSlab = slabs[lastSlabIndex];
            return targetSlab.AsSpan();
        }

        private class ChainStream : Stream
        {
            private static readonly List<ChainStream> INSTANCES = new ();

            private MemoryChain chain;
            private int slabIndex;
            private int slabOffset;
            private int totalRead;

            private int totalLength;

            private bool disposed;

            private ChainStream() { }

            public static ChainStream New(in MemoryChain chain)
            {
                lock (INSTANCES)
                {
                    ChainStream instance;

                    if (INSTANCES.Count > 0)
                    {
                        int index = INSTANCES.Count - 1;
                        instance = INSTANCES[index]!;
                        INSTANCES.RemoveAt(index);
                    }
                    else
                        instance = new ChainStream();

                    instance.chain = chain;
                    instance.slabIndex = 0;
                    instance.slabOffset = 0;
                    instance.totalRead = 0;

                    instance.totalLength = chain.TotalLength;

                    instance.disposed = false;

                    return instance;
                }
            }

            public override void Flush()
            {
                // Ignore
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (buffer == null)
                    throw new ArgumentNullException(nameof(buffer));

                if (offset < 0 || count < 0 || offset + count > buffer.Length)
                    throw new ArgumentOutOfRangeException(nameof(offset), $"Buffer length: {buffer.Length}, offset: {offset}, count: {count}");

                if (slabIndex >= chain.slabs.Count || totalRead >= totalLength)
                    return 0;

                int bytesRead = 0;

                while (count > 0 && slabIndex < chain.slabs.Count)
                {
                    Span<byte> currentSlab = chain.slabs[slabIndex].AsSpan();
                    int slabSize = currentSlab.Length;
                    int validDataInSlab = chain.IsLastSlab(slabIndex) ? slabSize - chain.leftSpaceInLast : slabSize;

                    if (slabOffset >= validDataInSlab)
                    {
                        slabIndex++;
                        slabOffset = 0;
                        continue;
                    }

                    int bytesAvailable = validDataInSlab - slabOffset;

                    if (bytesAvailable <= 0)
                        break;

                    int bytesToCopy = Math.Min(count, bytesAvailable);

                    if (bytesToCopy <= 0 || slabOffset + bytesToCopy > currentSlab.Length)
                        throw new ArgumentOutOfRangeException(nameof(bytesToCopy), $"Invalid bytesToCopy: {bytesToCopy}, slabOffset: {slabOffset}, slabSize: {currentSlab.Length}");

                    currentSlab.Slice(slabOffset, bytesToCopy).CopyTo(buffer.AsSpan(offset, bytesToCopy));

                    offset += bytesToCopy;
                    count -= bytesToCopy;
                    bytesRead += bytesToCopy;
                    slabOffset += bytesToCopy;
                    totalRead += bytesToCopy;
                }

                return bytesRead;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                long newPos = origin switch
                              {
                                  SeekOrigin.Begin => offset,
                                  SeekOrigin.Current => totalRead + offset,
                                  SeekOrigin.End => totalLength + offset,
                                  _ => throw new ArgumentOutOfRangeException(nameof(origin), "Invalid seek origin")
                              };

                if (newPos < 0 || newPos > totalLength)
                    throw new ArgumentOutOfRangeException(nameof(offset), $"Seek position is out of bounds, offset {offset}, newPos {newPos}, total length {totalLength}");

                totalRead = (int)newPos;
                slabIndex = 0;
                slabOffset = 0;
                int remaining = (int)newPos;

                foreach (var slab in chain.slabs)
                {
                    int slabSize = slab.AsSpan().Length;
                    int validDataInSlab = chain.IsLastSlab(slabIndex) ? slabSize - chain.leftSpaceInLast : slabSize;

                    if (remaining < validDataInSlab)
                    {
                        slabOffset = remaining;
                        break;
                    }

                    remaining -= validDataInSlab;
                    slabIndex++;
                }

                return totalRead;
            }

            public override void SetLength(long value) =>
                throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) =>
                throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                lock (INSTANCES)
                {
                    base.Dispose(disposing);

                    if (disposed)
                        return;

                    chain.Dispose();

                    chain = EMPTY;
                    slabIndex = 0;
                    slabOffset = 0;
                    totalRead = 0;
                    INSTANCES.Add(this);

                    disposed = true;
                }
            }

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => totalLength;

            public override long Position
            {
                get => totalRead;
                set => Seek(value, SeekOrigin.Begin);
            }
        }
    }

    public struct ChainMemoryIterator : IMemoryIterator
    {
        public static readonly ChainMemoryIterator EMPTY = new ((UnmanagedMemoryManager<byte>)null!);

        private readonly IntPtr buffer;
        private readonly UnmanagedMemoryManager<byte> unmanagedMemoryManager;
        private readonly IReadOnlyList<SlabItem> slabItems;
        private readonly int slabsCount;
        private readonly int leftInLastSlab;
        private readonly int totalLength;
        private int index;
        private bool disposed;

        internal ChainMemoryIterator(MemoryChain memoryChain) : this()
        {
            int size = ISlabAllocator.SHARED.Info.ChunkSize;
            buffer = NativeAlloc.Malloc((nuint)size);

            unsafe { unmanagedMemoryManager = UnmanagedMemoryManager<byte>.New(buffer.ToPointer()!, size); }

            slabItems = memoryChain.slabs;
            slabsCount = memoryChain.slabs.Count;
            leftInLastSlab = memoryChain.leftSpaceInLast;
            totalLength = memoryChain.TotalLength;

            index = -1;
            disposed = false;

            if (memoryChain.slabs.Count > 0 && memoryChain.slabs[0].chunkSize != unmanagedMemoryManager.Memory.Length)
                throw new Exception("Buffers have different sizes");
        }

        private ChainMemoryIterator(UnmanagedMemoryManager<byte>? empty)
        {
            buffer = IntPtr.Zero;
            unmanagedMemoryManager = empty!;

            slabItems = ArraySegment<SlabItem>.Empty;
            slabsCount = 0;
            leftInLastSlab = 0;
            totalLength = 0;

            index = -1;
            disposed = false;
        }

        public void Dispose()
        {
            if (unmanagedMemoryManager == null)
                return;

            if (disposed)
            {
                ReportHub.LogError(ReportCategory.ALLOCATORS, $"Attempt to dispose twice {nameof(ChainMemoryIterator)}");
                return;
            }

            disposed = true;
            NativeAlloc.Free(buffer);
            UnmanagedMemoryManager<byte>.Release(unmanagedMemoryManager);
        }

        private readonly bool IsLast() =>
            slabsCount - 1 == index;

        public readonly ReadOnlyMemory<byte> Current
        {
            get
            {
                var slab = slabItems[index].AsSpan();
                int bytesToRead = IsLast() ? slab.Length - leftInLastSlab : slab.Length;
                slab.Slice(0, bytesToRead).CopyTo(unmanagedMemoryManager.Memory.Span);
                return unmanagedMemoryManager.Memory.Slice(0, bytesToRead);
            }
        }

        public readonly int? TotalSize => totalLength;

        public bool MoveNext()
        {
            index++;
            return index < slabsCount;
        }
    }
}
