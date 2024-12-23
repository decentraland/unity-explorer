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
    }
}
