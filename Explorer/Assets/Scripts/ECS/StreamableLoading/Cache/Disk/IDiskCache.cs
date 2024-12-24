using Cysharp.Threading.Tasks;
using System;
using System.Buffers;
using System.Threading;
using Utility.Types;

namespace ECS.StreamableLoading.Cache.Disk
{
    public interface IDiskCache
    {
        UniTask<EnumResult<TaskError>> PutAsync(string key, string extension, ReadOnlyMemory<byte> data, CancellationToken token);

        UniTask<EnumResult<IMemoryOwner<byte>?, TaskError>> ContentAsync(string key, string extension, CancellationToken token);

        UniTask<EnumResult<TaskError>> RemoveAsync(string key, string extension, CancellationToken token);

        class Fake : IDiskCache
        {
            public UniTask<EnumResult<TaskError>> PutAsync(string key, string extension, ReadOnlyMemory<byte> data, CancellationToken token) =>
                UniTask.FromResult(EnumResult<TaskError>.ErrorResult(TaskError.MessageError, "It's fake"));

            public UniTask<EnumResult<IMemoryOwner<byte>?, TaskError>> ContentAsync(string key, string extension, CancellationToken token) =>
                UniTask.FromResult(EnumResult<IMemoryOwner<byte>?, TaskError>.SuccessResult(null));

            public UniTask<EnumResult<TaskError>> RemoveAsync(string key, string extension, CancellationToken token) =>
                UniTask.FromResult(EnumResult<TaskError>.SuccessResult());
        }
    }

    public interface IDiskCache<T>
    {
        UniTask<EnumResult<TaskError>> PutAsync(string key, string extension, T data, CancellationToken token);

        UniTask<EnumResult<Option<T>, TaskError>> ContentAsync(string key, string extension, CancellationToken token);

        UniTask<EnumResult<TaskError>> RemoveAsync(string key, string extension, CancellationToken token);
    }

    public interface IDiskSerializer<T>
    {
        UniTask<IMemoryOwner<byte>> Serialize(T data, CancellationToken token);

        /// <param name="data">Takes ownership of MemoryOwner</param>
        UniTask<T> Deserialize(IMemoryOwner<byte> data, CancellationToken token);
    }
}
