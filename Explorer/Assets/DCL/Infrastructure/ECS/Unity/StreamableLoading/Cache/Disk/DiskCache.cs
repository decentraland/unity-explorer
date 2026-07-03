// TRUST_WEBGL_THREAD_SAFETY_FLAG
#if !UNITY_WEBGL

using Cysharp.Threading.Tasks;
using DCL.Optimization.Hashing;
using DCL.Utility.Types;
using DCL.WebRequests.Dumper;
using ECS.StreamableLoading.Cache.Disk.CleanUp;
using ECS.StreamableLoading.Cache.Disk.Lock;
using System;
using System.IO;
using System.Threading;
using DCL.Diagnostics;
using Utility.Multithreading;

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

        public async UniTask<EnumResult<TaskError>> PutAsync<Ti>(HashKey key, string extension, Ti data, CancellationToken token) where Ti: IMemoryIterator
        {
            if (WebRequestsDebugControl.DisableCache)
                return EnumResult<TaskError>.SuccessResult();

            await using var scope = await ExecuteOnThreadPoolScope.NewScopeAsync();
            string fileName = HashNamings.HashNameFrom(key, extension);
            string path = PathFrom(fileName);

            // Stream into a temporary file and swap it into the final path only once fully written.
            // A write interrupted by cancellation or a crash must never leave a truncated file at the
            // final path: it would be served as a valid cache entry on every subsequent read.
            string tempPath = PathFrom(fileName + IDiskCache.TEMP_FILE_SUFFIX);
            bool existed = File.Exists(path);

            try
            {
                using var _ = filesLock.TryLock(path, out bool success);

                if (success == false)
                    return EnumResult<TaskError>.ErrorResult(TaskError.MessageError, "File is being used");

                {
                    await using var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write);

                    while (data.MoveNext())
                    {
                        var chunk = data.Current;
                        await stream.WriteAsync(chunk, token);
                    }
                }

                if (File.Exists(path))
                    File.Delete(path);

                File.Move(tempPath, path);

                diskCleanUp.CleanUpIfNeeded();
            }
            catch (TimeoutException)
            {
                DeleteNoThrow(tempPath);
                return EnumResult<TaskError>.ErrorResult(TaskError.Timeout);
            }
            catch (OperationCanceledException)
            {
                DeleteNoThrow(tempPath);
                return EnumResult<TaskError>.ErrorResult(TaskError.Cancelled);
            }
            catch (Exception e)
            {
                DeleteNoThrow(tempPath);
                return EnumResult<TaskError>.ErrorResult(TaskError.UnexpectedException, e.Message ?? string.Empty);
            }

            ReportHub.Log(
                ReportCategory.STREAMABLE_LOADING,
                $"[DiskCache] WRITE OK {(existed ? "OVERWRITE" : "CREATE")} path='{path}'"
            );
            
            return EnumResult<TaskError>.SuccessResult();
        }

        public async UniTask<EnumResult<SlicedOwnedMemory<byte>?, TaskError>> ContentAsync(HashKey key, string extension, CancellationToken token)
        {
            if (WebRequestsDebugControl.DisableCache)
                return EnumResult<SlicedOwnedMemory<byte>?, TaskError>.SuccessResult(null);

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
                    data = new SlicedOwnedMemory<byte>((int)stream.Length);
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
            if (WebRequestsDebugControl.DisableCache)
                return EnumResult<TaskError>.SuccessResult();

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

        private static void DeleteNoThrow(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception)
            {
                // Best effort only: an orphaned temp file is harmless and is overwritten by the next write.
            }
        }
    }

    public class DiskCache<T, Ts> : IDiskCache<T> where Ts: IMemoryIterator
    {
        private readonly IDiskCache diskCache;
        private readonly IDiskSerializer<T, Ts> serializer;

        public DiskCache(IDiskCache diskCache, IDiskSerializer<T, Ts> serializer)
        {
            this.diskCache = diskCache;
            this.serializer = serializer;
        }

        public async UniTask<EnumResult<TaskError>> PutAsync(HashKey key, string extension, T data, CancellationToken token)
        {
            using var iterator = serializer.Serialize(data);
            return await diskCache.PutAsync(key, extension, iterator, token);
        }

        public async UniTask<EnumResult<Option<T>, TaskError>> ContentAsync(HashKey key, string extension, CancellationToken token)
        {
            var result = await diskCache.ContentAsync(key, extension, token);

            if (result.Success == false)
                return EnumResult<Option<T>, TaskError>.ErrorResult(result.Error!.Value.State, result.Error.Value.Message!);

            SlicedOwnedMemory<byte>? data = result.Value;

            if (data == null)
                return EnumResult<Option<T>, TaskError>.SuccessResult(Option<T>.None);

            T resultDeserialize;

            try { resultDeserialize = await serializer.DeserializeAsync(data.Value, token); }
            catch (OperationCanceledException) { return EnumResult<Option<T>, TaskError>.ErrorResult(TaskError.Cancelled); }
            catch (Exception e)
            {
                // The stored entry is corrupt (e.g. a write interrupted by an older client):
                // remove the file so subsequent reads don't deserialize the same corrupt entry again
                await diskCache.RemoveAsync(key, extension, CancellationToken.None);
                return EnumResult<Option<T>, TaskError>.ErrorResult(TaskError.UnexpectedException, e.Message ?? string.Empty);
            }

            if(resultDeserialize != null)
                return EnumResult<Option<T>, TaskError>.SuccessResult(Option<T>.Some(resultDeserialize));

            return EnumResult<Option<T>, TaskError>.SuccessResult(Option<T>.None);
        }

        public UniTask<EnumResult<TaskError>> RemoveAsync(HashKey key, string extension, CancellationToken token) =>
            diskCache.RemoveAsync(key, extension, token);
    }
}

#endif
