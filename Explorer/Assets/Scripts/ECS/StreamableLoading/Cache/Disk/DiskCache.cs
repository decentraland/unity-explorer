using Cysharp.Threading.Tasks;
using DCL.Optimization.Hashing;
using ECS.StreamableLoading.Cache.Disk.CleanUp;
using ECS.StreamableLoading.Cache.Disk.Lock;
using System;
using System.Buffers;
using System.IO;
using System.Threading;
using Utility.Multithreading;
using Utility.Types;

namespace ECS.StreamableLoading.Cache.Disk
{
    public class DiskCache : IDiskCache
    {
        private readonly CacheDirectory directory;
        private readonly FilesLock filesLock;
        private readonly IDiskCleanUp diskCleanUp;

        public DiskCache(CacheDirectory directory, FilesLock filesLock, IDiskCleanUp diskCleanUp)
        {
            this.directory = directory;
            this.filesLock = filesLock;
            this.diskCleanUp = diskCleanUp;
        }

        public async UniTask<EnumResult<TaskError>> PutAsync(HashKey key, string extension, ReadOnlyMemory<byte> data, CancellationToken token)
        {
            await using var scope = await ExecuteOnThreadPoolScope.NewScopeAsync();

            try
            {
                string path = PathFrom(key, extension);
                using var _ = filesLock.TryLock(path, out bool success);

                if (success == false)
                    return EnumResult<TaskError>.ErrorResult(TaskError.MessageError, "File is being used");

                await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
                await stream.WriteAsync(data, token);
                diskCleanUp.CleanUpIfNeeded();
            }
            catch (TimeoutException) { return EnumResult<TaskError>.ErrorResult(TaskError.Timeout); }
            catch (OperationCanceledException) { return EnumResult<TaskError>.ErrorResult(TaskError.Cancelled); }
            catch (Exception e) { return EnumResult<TaskError>.ErrorResult(TaskError.UnexpectedException, e.Message ?? string.Empty); }

            return EnumResult<TaskError>.SuccessResult();
        }

        public async UniTask<EnumResult<SlicedOwnedMemory<byte>?, TaskError>> ContentAsync(HashKey key, string extension, CancellationToken token)
        {
            await using var scope = await ExecuteOnThreadPoolScope.NewScopeAsync();

            try
            {
                string fileName = HashNamings.HashNameFrom(key, extension);
                string path = PathFrom(fileName);

                using var fileScope = filesLock.TryLock(path, out bool success);

                if (success == false)
                    return EnumResult<SlicedOwnedMemory<byte>?, TaskError>.SuccessResult(null);

                if (File.Exists(path) == false)
                    return EnumResult<SlicedOwnedMemory<byte>?, TaskError>.SuccessResult(null);

                SlicedOwnedMemory<byte> data;

                {
                    await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                    data = new SlicedOwnedMemory<byte>(MemoryPool<byte>.Shared!.Rent((int)stream.Length)!, (int)stream.Length);
                    int _ = await stream.ReadAsync(data.Memory, token);
                }

                diskCleanUp.NotifyUsed(fileName);
                return EnumResult<SlicedOwnedMemory<byte>?, TaskError>.SuccessResult(data);
            }
            catch (TimeoutException) { return EnumResult<SlicedOwnedMemory<byte>?, TaskError>.ErrorResult(TaskError.Timeout); }
            catch (OperationCanceledException) { return EnumResult<SlicedOwnedMemory<byte>?, TaskError>.ErrorResult(TaskError.Cancelled); }
            catch (Exception e) { return EnumResult<SlicedOwnedMemory<byte>?, TaskError>.ErrorResult(TaskError.UnexpectedException, e.Message ?? string.Empty); }
        }

        public async UniTask<EnumResult<TaskError>> RemoveAsync(HashKey key, string extension, CancellationToken token)
        {
            await using var scope = await ExecuteOnThreadPoolScope.NewScopeAsync();

            try
            {
                string path = PathFrom(key, extension);
                using var fileScope = filesLock.TryLock(path, out bool success);

                if (success == false)
                    return EnumResult<TaskError>.ErrorResult(TaskError.MessageError, "File is being used");

                if (File.Exists(path)) File.Delete(path);
                return EnumResult<TaskError>.SuccessResult();
            }
            catch (Exception e) { return EnumResult<TaskError>.ErrorResult(TaskError.UnexpectedException, e.Message ?? string.Empty); }
        }

        private string PathFrom(HashKey key, string extension)
        {
            string hashName = HashNamings.HashNameFrom(key, extension);
            return PathFrom(hashName);
        }

        private string PathFrom(string fileName)
        {
            string fullPath = Path.Combine(directory.Path, fileName);
            return fullPath;
        }
    }

    public class DiskCache<T> : IDiskCache<T> where T: class
    {
        private readonly IDiskCache diskCache;
        private readonly IDiskSerializer<T> serializer;

        public DiskCache(IDiskCache diskCache, IDiskSerializer<T> serializer)
        {
            this.diskCache = diskCache;
            this.serializer = serializer;
        }

        public async UniTask<EnumResult<TaskError>> PutAsync(HashKey key, string extension, T data, CancellationToken token)
        {
            using SlicedOwnedMemory<byte> serializedData = await serializer.SerializeAsync(data, token);
            return await diskCache.PutAsync(key, extension, serializedData.Memory, token);
        }

        public async UniTask<EnumResult<Option<T>, TaskError>> ContentAsync(HashKey key, string extension, CancellationToken token)
        {
            var result = await diskCache.ContentAsync(key, extension, token);

            if (result.Success == false)
                return EnumResult<Option<T>, TaskError>.ErrorResult(result.Error!.Value.State, result.Error.Value.Message!);

            SlicedOwnedMemory<byte>? data = result.Value;

            if (data == null)
                return EnumResult<Option<T>, TaskError>.SuccessResult(Option<T>.None);

            T deserializedValue = await serializer.DeserializeAsync(data.Value, token);
            return EnumResult<Option<T>, TaskError>.SuccessResult(Option<T>.Some(deserializedValue));
        }

        public UniTask<EnumResult<TaskError>> RemoveAsync(HashKey key, string extension, CancellationToken token) =>
            diskCache.RemoveAsync(key, extension, token);
    }
}
