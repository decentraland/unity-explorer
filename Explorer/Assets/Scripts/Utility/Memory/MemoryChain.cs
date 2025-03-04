using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Pool;

namespace Utility.Memory
{
    public struct MemoryChain : IDisposable
    {
        public static readonly MemoryChain EMPTY = new ();

        /// <summary>
        /// Doesn't own allocator
        /// </summary>
        private readonly ThreadSafeSlabAllocator<DynamicSlabAllocator> allocator;
        private readonly List<SlabItem> slabs;
        private int leftSpaceInLast;

        public MemoryChain(ThreadSafeSlabAllocator<DynamicSlabAllocator> allocator) : this()
        {
            this.allocator = allocator;
            slabs = ListPool<SlabItem>.Get()!;
            leftSpaceInLast = 0;
        }

        public readonly void Dispose()
        {
            if (allocator == null)
                return;

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

        private void AllocateNewSlab()
        {
            slabs.Add(allocator.Allocate());
            leftSpaceInLast = LastSpan().Length;
        }

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

                    return instance;
                }
            }

            public override void Flush()
            {
                // Ignore
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));

                if (offset < 0 || count < 0 || offset + count > buffer.Length)
                    throw new ArgumentOutOfRangeException($"Buffer length: {buffer.Length}, offset: {offset}, count: {count}");

                if (slabIndex >= chain.slabs.Count)
                    return 0;

                int bytesRead = 0;
                int totalValidData = TotalValidDataLength();

                while (count > 0 && slabIndex < chain.slabs.Count)
                {
                    Span<byte> currentSlab = chain.slabs[slabIndex].AsSpan();

                    int slabSize = currentSlab.Length;
                    int validDataInSlab = IsLastSlab(slabIndex) ? slabSize - chain.leftSpaceInLast : slabSize;

                    if (totalRead >= totalValidData)
                        break;

                    int bytesAvailable = validDataInSlab - slabOffset;
                    int bytesToCopy = Math.Min(count, bytesAvailable);

                    currentSlab.Slice(slabOffset, bytesToCopy).CopyTo(buffer.AsSpan(offset, bytesToCopy));

                    offset += bytesToCopy;
                    count -= bytesToCopy;
                    bytesRead += bytesToCopy;
                    slabOffset += bytesToCopy;
                    totalRead += bytesToCopy;

                    if (slabOffset >= validDataInSlab)
                    {
                        slabIndex++;
                        slabOffset = 0;
                    }
                }

                return bytesRead;
            }

            private int TotalValidDataLength()
            {
                int totalValid = 0;

                for (int i = 0; i < chain.slabs.Count; i++)
                {
                    int slabSize = chain.slabs[i].AsSpan().Length;
                    totalValid += IsLastSlab(i) ? slabSize - chain.leftSpaceInLast : slabSize;
                }

                return totalValid;
            }

            private bool IsLastSlab(int index) =>
                (index == chain.slabs.Count - 1);

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);

                if (disposing == false)
                {
                    chain.Dispose();

                    lock (INSTANCES)
                    {
                        chain = EMPTY;
                        slabIndex = 0;
                        slabOffset = 0;
                        totalRead = 0;
                        INSTANCES.Add(this);
                    }
                }
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;

            public override long Length => TotalValidDataLength();

            public override long Position
            {
                get => totalRead;
                set => throw new NotSupportedException();
            }
        }
    }
}
