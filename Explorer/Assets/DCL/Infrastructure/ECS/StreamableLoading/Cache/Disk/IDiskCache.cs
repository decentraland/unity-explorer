using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Hashing;
using DCL.Optimization.ThreadSafePool;
using System;
using System.Buffers;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Utility.Ownership;
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
        UniTask<Result<T>> DeserializeAsync([TakesOwnership] SlicedOwnedMemory<byte> data, CancellationToken token);
    }

    public readonly struct SlicedOwnedMemory<T> : IDisposable where T: unmanaged
    {
        public static readonly unsafe SlicedOwnedMemory<T> EMPTY = new (UnmanagedMemoryManager<T>.New((void*)0, 0));

        private readonly UnmanagedMemoryManager<T> memoryManager;
        private readonly IntPtr ptr;

        public Memory<T> Memory => memoryManager.Memory;

        public SlicedOwnedMemory(int length)
        {
            unsafe
            {
                void* memory = UnsafeUtility.Malloc(length, 64, Allocator.Persistent);
                ptr = new IntPtr(memory);
                memoryManager = UnmanagedMemoryManager<T>.New(memory, length);
            }
        }

        private SlicedOwnedMemory(UnmanagedMemoryManager<T> memoryManager)
        {
            this.memoryManager = memoryManager;
            ptr = IntPtr.Zero;
        }

        public void Dispose()
        {
            if (ptr == IntPtr.Zero)
                return;

            unsafe { UnsafeUtility.Free(ptr.ToPointer(), Allocator.Persistent); }

            UnmanagedMemoryManager<T>.Release(memoryManager);
        }
    }

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

    public interface IMemoryIterator : IDisposable
    {
        ReadOnlyMemory<byte> Current { get; }

        int? TotalSize { get; }

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
            int lengthToRead = buffer.Length;

            int endOfRead = offset + lengthToRead;

            if (endOfRead > data.Length)
                endOfRead = data.Length;

            var slice = data.Slice(offset, endOfRead - offset);
            slice.CopyTo(buffer.Span);
            return slice.Length;
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

        public int? TotalSize => null;

        public bool MoveNext()
        {
            // index == -1 because it doesn't make a sense to put an uniteratable sequence
            if (index == -1)
            {
                index++;
                return true;
            }

            index++;
            return canMoveNextDelegate(source, index, buffer.Length);
        }

        public void Dispose()
        {
            SerializeMemoryIterator.POOL.Release(buffer);
        }
    }

    public struct SingleMemoryIterator : IMemoryIterator
    {
        private readonly byte[] data;
        private int index;

        public SingleMemoryIterator(byte[] data) : this()
        {
            this.data = data;
            index = -1;
        }

        public ReadOnlyMemory<byte> Current => data;

        public int? TotalSize => data.Length;

        public bool MoveNext()
        {
            if (index == -1)
            {
                index = 0;
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            //ignore
        }
    }
}
