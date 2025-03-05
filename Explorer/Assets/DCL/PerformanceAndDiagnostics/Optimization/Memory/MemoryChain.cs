using Sentry;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Pool;

namespace DCL.Optimization.Memory
{
    public struct MemoryChain : IDisposable
    {
        public static readonly MemoryChain EMPTY = new ();

        /// <summary>
        /// Doesn't own allocator
        /// </summary>
        private readonly ThreadSafeSlabAllocator<DynamicSlabAllocator> allocator;
        internal readonly List<SlabItem> slabs;
        private int leftSpaceInLast;

#if UNITY_EDITOR || DEBUG
        private string? disposedBy;
#endif

        public readonly int TotalLength
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

        public MemoryChain(ThreadSafeSlabAllocator<DynamicSlabAllocator> allocator) : this()
        {
            this.allocator = allocator;
            slabs = ListPool<SlabItem>.Get()!;
            leftSpaceInLast = 0;
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

            for (int i = 0; i < slabs.Count; i++) allocator.Release(slabs[i]);
            ListPool<SlabItem>.Release(slabs);
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

                if (leftSpaceInLast == 0 && dataOffset < data.Length)
                    AllocateNewSlab();
            }
        }

        public void AppendData(in MemoryChain data)
        {
            for (int i = 0; i < data.slabs.Count - 1; i++) AppendData(data.slabs[i].AsSpan());
            AppendData(LastSpan().Slice(0, data.leftSpaceInLast));
        }

        public unsafe void AppendData(void* memory, int size)
        {
            AppendData(new Span<byte>(memory, size));
        }

        /// <summary>
        /// Doesn't borrow MemoryChain and returns a stream that reads from it
        /// </summary>
        /// <returns></returns>
        public readonly Stream AsStream() =>
            ChainStream.New(this);

        public readonly ChainMemoryIterator AsMemoryIterator() =>
            new (this);

        private void AllocateNewSlab()
        {
            slabs.Add(allocator.Allocate());
            leftSpaceInLast = LastSpan().Length;
        }

        private readonly bool IsLastSlab(int index) =>
            index == slabs.Count - 1;

        private readonly Span<byte> LastSpan()
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
                base.Dispose(disposing);

                if (!disposing)
                    lock (INSTANCES)
                    {
                        chain = EMPTY;
                        slabIndex = 0;
                        slabOffset = 0;
                        totalRead = 0;
                        INSTANCES.Add(this);
                    }
            }

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length { get; }

            public override long Position
            {
                get => totalRead;
                set => Seek(value, SeekOrigin.Begin);
            }
        }
    }

    public struct ChainMemoryIterator : IMemoryIterator
    {
        private readonly MemoryChain memoryChain;
        private readonly SlabItem buffer;
        private readonly UnmanagedMemoryManager<byte> unmanagedMemoryManager;
        private int index;

        internal ChainMemoryIterator(MemoryChain memoryChain) : this()
        {
            this.memoryChain = memoryChain;
            buffer = ISlabAllocator.SHARED.Allocate();

            unsafe { unmanagedMemoryManager = UnmanagedMemoryManager<byte>.New(buffer.ptr.ToPointer()!, buffer.chunkSize); }

            index = -1;

            if (memoryChain.slabs.Count > 0 && memoryChain.slabs[0].chunkSize != unmanagedMemoryManager.Memory.Length)
                throw new Exception("Buffers have different sizes");
        }

        public readonly void Dispose()
        {
            ISlabAllocator.SHARED.Release(buffer);
            UnmanagedMemoryManager<byte>.Release(unmanagedMemoryManager);
        }

        public readonly ReadOnlyMemory<byte> Current
        {
            get
            {
                var slab = memoryChain.slabs[index];
                slab.AsSpan().CopyTo(unmanagedMemoryManager.Memory.Span);
                return unmanagedMemoryManager.Memory;
            }
        }

        public readonly int? TotalSize => memoryChain.TotalLength;

        public bool MoveNext()
        {
            index++;
            return index < memoryChain.slabs.Count;
        }
    }
}
