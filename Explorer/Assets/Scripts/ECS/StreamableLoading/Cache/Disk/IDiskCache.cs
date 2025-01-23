using Cysharp.Threading.Tasks;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading;
using Utility.Types;

namespace ECS.StreamableLoading.Cache.Disk
{
    public interface IDiskCache
    {
        UniTask<EnumResult<TaskError>> PutAsync(string key, string extension, ReadOnlyMemory<byte> data, CancellationToken token);

        UniTask<EnumResult<SlicedOwnedMemory<byte>?, TaskError>> ContentAsync(string key, string extension, CancellationToken token);

        UniTask<EnumResult<TaskError>> RemoveAsync(string key, string extension, CancellationToken token);

        class Fake : IDiskCache
        {
            public UniTask<EnumResult<TaskError>> PutAsync(string key, string extension, ReadOnlyMemory<byte> data, CancellationToken token) =>
                UniTask.FromResult(EnumResult<TaskError>.ErrorResult(TaskError.MessageError, "It's fake"));

            public UniTask<EnumResult<SlicedOwnedMemory<byte>?, TaskError>> ContentAsync(string key, string extension, CancellationToken token) =>
                UniTask.FromResult(EnumResult<SlicedOwnedMemory<byte>?, TaskError>.SuccessResult(null));

            public UniTask<EnumResult<TaskError>> RemoveAsync(string key, string extension, CancellationToken token) =>
                UniTask.FromResult(EnumResult<TaskError>.SuccessResult());
        }
    }

    public interface IDiskCache<T>
    {
        UniTask<EnumResult<TaskError>> PutAsync(string key, string extension, T data, CancellationToken token);

        UniTask<EnumResult<Option<T>, TaskError>> ContentAsync(string key, string extension, CancellationToken token);

        UniTask<EnumResult<TaskError>> RemoveAsync(string key, string extension, CancellationToken token);

        class Null : IDiskCache<T>
        {
            public static readonly Null INSTANCE = new ();

            private Null() { }

            public UniTask<EnumResult<TaskError>> PutAsync(string key, string extension, T data, CancellationToken token) =>
                UniTask.FromResult(EnumResult<TaskError>.ErrorResult(TaskError.MessageError, "It's null"));

            public UniTask<EnumResult<Option<T>, TaskError>> ContentAsync(string key, string extension, CancellationToken token) =>
                UniTask.FromResult(EnumResult<Option<T>, TaskError>.SuccessResult(Option<T>.None));

            public UniTask<EnumResult<TaskError>> RemoveAsync(string key, string extension, CancellationToken token) =>
                UniTask.FromResult(EnumResult<TaskError>.SuccessResult());
        }
    }

    public interface IDiskSerializer<T>
    {
        UniTask<SlicedOwnedMemory<byte>> SerializeAsync(T data, CancellationToken token);

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
        }

        public void Dispose()
        {
            owner.Dispose();
        }
    }
}
