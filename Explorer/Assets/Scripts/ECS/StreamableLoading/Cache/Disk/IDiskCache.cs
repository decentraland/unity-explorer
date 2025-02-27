using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Hashing;
using DCL.Optimization.ThreadSafePool;
using System;
using System.Buffers;
using System.Threading;
using Utility.Types;

namespace ECS.StreamableLoading.Cache.Disk
{
    public interface IDiskCache
    {
        UniTask<EnumResult<TaskError>> PutAsync<Ti>(HashKey key, string extension, Ti data, CancellationToken token) where Ti: IMemoryIterator;

        UniTask<EnumResult<SlicedOwnedMemory<byte>?, TaskError>> ContentAsync(HashKey key, string extension, CancellationToken token);

        UniTask<EnumResult<TaskError>> RemoveAsync(HashKey key, string extension, CancellationToken token);

        class Fake : IDiskCache
        {
            public UniTask<EnumResult<TaskError>> PutAsync<Ti>(HashKey key, string extension, Ti data, CancellationToken token) where Ti: IMemoryIterator =>
                UniTask.FromResult(EnumResult<TaskError>.ErrorResult(TaskError.MessageError, "It's fake"));

            public UniTask<EnumResult<SlicedOwnedMemory<byte>?, TaskError>> ContentAsync(HashKey key, string extension, CancellationToken token) =>
                UniTask.FromResult(EnumResult<SlicedOwnedMemory<byte>?, TaskError>.SuccessResult(null));

            public UniTask<EnumResult<TaskError>> RemoveAsync(HashKey key, string extension, CancellationToken token) =>
                UniTask.FromResult(EnumResult<TaskError>.SuccessResult());
        }
    }

    public interface IDiskCache<T>
    {
        UniTask<EnumResult<TaskError>> PutAsync(HashKey key, string extension, T data, CancellationToken token);

        UniTask<EnumResult<Option<T>, TaskError>> ContentAsync(HashKey key, string extension, CancellationToken token);

        UniTask<EnumResult<TaskError>> RemoveAsync(HashKey key, string extension, CancellationToken token);

        class Null : IDiskCache<T>
        {
            public static readonly Null INSTANCE = new ();

            private Null() { }

            public UniTask<EnumResult<TaskError>> PutAsync(HashKey key, string extension, T data, CancellationToken token) =>
                UniTask.FromResult(EnumResult<TaskError>.ErrorResult(TaskError.MessageError, "It's null"));

            public UniTask<EnumResult<Option<T>, TaskError>> ContentAsync(HashKey key, string extension, CancellationToken token) =>
                UniTask.FromResult(EnumResult<Option<T>, TaskError>.SuccessResult(Option<T>.None));

            public UniTask<EnumResult<TaskError>> RemoveAsync(HashKey key, string extension, CancellationToken token) =>
                UniTask.FromResult(EnumResult<TaskError>.SuccessResult());
        }
    }

    public interface IDiskSerializer<T, Ti> where Ti: IMemoryIterator
    {
        Ti Serialize(T data);

        /// <param name="data">Takes ownership of Memory and is responsible for its disposal</param>
        UniTask<T> DeserializeAsync(SlicedOwnedMemory<byte> data, CancellationToken token);
    }

    /// <summary>
    /// Required because MemoryOwner does not guarantee that the memory is the exact size
    /// </summary>
    public readonly struct SlicedOwnedMemory<T> : IDisposable
    {
        private readonly IMemoryOwner<T> owner;
        private readonly int length;

        public Memory<T> Memory => owner.Memory.Slice(0, length);

        public SlicedOwnedMemory(IMemoryOwner<T> owner, int length)
        {
            this.owner = owner;
            this.length = length;

            ReportHub.LogProductionInfo($"Request to allocate memory with size: {Utility.ByteSize.ToReadableString((ulong)length)}");
        }

        public void Dispose()
        {
            owner.Dispose();
        }
    }

    public interface IMemoryIterator : IDisposable
    {
        ReadOnlyMemory<byte> Current { get; }

        bool MoveNext();
    }

    public static class SerializeMemoryIterator
    {
        public const int CHUNK_SIZE = 128 * 1024; //128 kb

        public static readonly ThreadSafeObjectPool<byte[]> POOL = new (static () => new byte[CHUNK_SIZE]);

        public static bool CanReadNextData(int index, int dataLength, int bufferLength)
        {
            int nextBufferStartRead = bufferLength * index;
            return nextBufferStartRead < dataLength;
        }

        public static int ReadNextData(int index, ReadOnlySpan<byte> data, Memory<byte> buffer)
        {
            int offset = buffer.Length * index;
            int length = buffer.Length;

            // Clamp if buffer length exceeds remaining data length
            if (offset + length >= data.Length)
                length = data.Length - offset;

            data.Slice(offset, length).CopyTo(buffer.Span);
            return length;
        }
    }

    public struct SerializeMemoryIterator<T> : IMemoryIterator
    {
        /// <summary>
        /// Returns written byte count
        /// </summary>
        public delegate int FillBufferDelegate(T source, int currentIndex, Memory<byte> buffer);

        public delegate bool CanMoveNextDelegate(T source, int currentIndex, int bufferSize);

        private readonly T source;
        private readonly byte[] buffer;
        private readonly FillBufferDelegate fillBufferDelegate;
        private readonly CanMoveNextDelegate canMoveNextDelegate;

        /// <summary>
        /// Starts with -1
        /// </summary>
        private int index;

        private SerializeMemoryIterator(T source, FillBufferDelegate fillBufferDelegate, CanMoveNextDelegate canMoveNextDelegate)
        {
            this.source = source;
            this.fillBufferDelegate = fillBufferDelegate;
            this.canMoveNextDelegate = canMoveNextDelegate;
            index = -1;
            buffer = SerializeMemoryIterator.POOL.Get();
        }

        public static SerializeMemoryIterator<T> New(T source, FillBufferDelegate fillBufferDelegate, CanMoveNextDelegate canMoveNextFunc) =>
            new (source, fillBufferDelegate, canMoveNextFunc);

        public ReadOnlyMemory<byte> Current
        {
            get
            {
                if (index == -1)
                    return ReadOnlyMemory<byte>.Empty;

                int writtenCount = fillBufferDelegate(source, index, buffer);
                return buffer.AsMemory(0, writtenCount);
            }
        }

        public bool MoveNext()
        {
            // index == -1 because it doesn't make a sense to put an uniteratable sequence
            bool can = index == -1 || canMoveNextDelegate(source, index, buffer.Length);
            if (can) index++;
            return can;
        }

        public void Dispose()
        {
            SerializeMemoryIterator.POOL.Release(buffer);
        }
    }
}
