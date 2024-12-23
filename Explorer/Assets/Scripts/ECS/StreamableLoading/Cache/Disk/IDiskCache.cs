using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using Utility.Types;

namespace DCL.Caches.Disk
{
    public interface IDiskCache
    {
        UniTask<EnumResult<TaskError>> PutAsync(string key, string extension, ReadOnlyMemory<byte> data, CancellationToken token);

        UniTask<EnumResult<byte[]?, TaskError>> ContentAsync(string key, string extension, CancellationToken token);

        UniTask<EnumResult<TaskError>> RemoveAsync(string key, string extension, CancellationToken token);
    }

    public interface IDiskCache<T>
    {
        UniTask<EnumResult<TaskError>> PutAsync(string key, string extension, T data, CancellationToken token);

        UniTask<EnumResult<Option<T>, TaskError>> ContentAsync(string key, string extension, CancellationToken token);

        UniTask<EnumResult<TaskError>> RemoveAsync(string key, string extension, CancellationToken token);
    }

    public interface IDiskSerializer<T>
    {
        UniTask<byte[]> Serialize(T data, CancellationToken token);

        UniTask<T> Deserialize(byte[] data, CancellationToken token);
    }
}
