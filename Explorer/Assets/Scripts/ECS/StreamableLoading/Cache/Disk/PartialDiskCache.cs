using Cysharp.Threading.Tasks;
using DCL.Optimization.Hashing;
using ECS.StreamableLoading.Cache.Disk.CleanUp;
using ECS.StreamableLoading.Cache.Disk.Lock;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Utility.Multithreading;
using Utility.Types;

namespace ECS.StreamableLoading.Cache.Disk
{
    public class PartialDiskCache : IPartialDiskCache
    {
        private readonly CacheDirectory cacheDirectory;
        private readonly FilesLock filesLock;
        private readonly IDiskCleanUp diskCleanUp;
        private readonly ConcurrentDictionary<string, MutexSlim<PartialFile>> cache = new ();
        private readonly SemaphoreSlim semaphoreSlim = new (1, 1);

        public PartialDiskCache(CacheDirectory cacheDirectory, FilesLock filesLock, IDiskCleanUp diskCleanUp)
        {
            this.cacheDirectory = cacheDirectory;
            this.filesLock = filesLock;
            this.diskCleanUp = diskCleanUp;
        }

        public async UniTask<EnumResult<MutexSlim<PartialFile>, TaskError>> PartialFileAsync(HashKey key, string extension, CancellationToken ct)
        {
            await using var scope = await ExecuteOnThreadPoolScope.NewScopeAsync();

            try
            {
                await semaphoreSlim.WaitAsync(ct);
                string path = cacheDirectory.PathFor(key, extension);

                if (cache.TryGetValue(path, out var cached))
                {
                    if (cached!.Disposed == false)
                        return EnumResult<MutexSlim<PartialFile>, TaskError>.SuccessResult(cached);

                    cache.TryRemove(path, out var _);
                }

                using var _ = filesLock.TryLock(path, out bool success);

                if (success == false)
                    return EnumResult<MutexSlim<PartialFile>, TaskError>.ErrorResult(TaskError.MessageError, "File is being used");

                var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                var meta = await PartialFile.Meta.FromStreamAsync(stream);

                // TODO ensure no concurrency
                // diskCleanUp.CleanUpIfNeeded();
                var fileKey = key.Copy();
                var file = new PartialFile(fileKey, stream, meta);
                var mutex = new MutexSlim<PartialFile>(file);

                cache[path] = mutex;

                return EnumResult<MutexSlim<PartialFile>, TaskError>.SuccessResult(mutex);
            }
            catch (TimeoutException) { return EnumResult<MutexSlim<PartialFile>, TaskError>.ErrorResult(TaskError.Timeout); }
            catch (OperationCanceledException) { return EnumResult<MutexSlim<PartialFile>, TaskError>.ErrorResult(TaskError.Cancelled); }
            catch (Exception e) { return EnumResult<MutexSlim<PartialFile>, TaskError>.ErrorResult(TaskError.UnexpectedException, e.Message ?? string.Empty); }
            finally { semaphoreSlim.Release(); }
        }
    }
}
