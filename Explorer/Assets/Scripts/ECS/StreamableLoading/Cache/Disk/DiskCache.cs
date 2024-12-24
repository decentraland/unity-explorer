using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Buffers;
using System.IO;
using System.Threading;
using Utility.Types;

namespace ECS.StreamableLoading.Cache.Disk
{
    public class DiskCache : IDiskCache
    {
        private readonly string dirPath;

        public DiskCache(string dirPath)
        {
            this.dirPath = dirPath;
            if (Directory.Exists(dirPath) == false) Directory.CreateDirectory(this.dirPath);

            ReportHub.Log(
                ReportCategory.DEBUG,
                $"DiskCache: use directory at {Path.Combine(Environment.CurrentDirectory, dirPath)}"
            );
        }

        public async UniTask<EnumResult<TaskError>> PutAsync(string key, string extension, ReadOnlyMemory<byte> data, CancellationToken token)
        {
            try
            {
                string path = PathFrom(key, extension);
                await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
                await stream.WriteAsync(data, token);
            }
            catch (TimeoutException) { return EnumResult<TaskError>.ErrorResult(TaskError.Timeout); }
            catch (OperationCanceledException) { return EnumResult<TaskError>.ErrorResult(TaskError.Cancelled); }
            catch (Exception e) { return EnumResult<TaskError>.ErrorResult(TaskError.UnexpectedException, e.Message ?? string.Empty); }

            return EnumResult<TaskError>.SuccessResult();
        }

        public async UniTask<EnumResult<IMemoryOwner<byte>?, TaskError>> ContentAsync(string key, string extension, CancellationToken token)
        {
            try
            {
                string path = PathFrom(key, extension);

                if (File.Exists(path) == false)
                    return EnumResult<IMemoryOwner<byte>?, TaskError>.SuccessResult(null);

                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                var data = MemoryPool<byte>.Shared!.Rent((int)stream.Length)!;

                int _ = await stream.ReadAsync(data.Memory, token);
                return EnumResult<IMemoryOwner<byte>?, TaskError>.SuccessResult(data);
            }
            catch (TimeoutException) { return EnumResult<IMemoryOwner<byte>?, TaskError>.ErrorResult(TaskError.Timeout); }
            catch (OperationCanceledException) { return EnumResult<IMemoryOwner<byte>?, TaskError>.ErrorResult(TaskError.Cancelled); }
            catch (Exception e) { return EnumResult<IMemoryOwner<byte>?, TaskError>.ErrorResult(TaskError.UnexpectedException, e.Message ?? string.Empty); }
        }

        public UniTask<EnumResult<TaskError>> RemoveAsync(string key, string extension, CancellationToken token)
        {
            try
            {
                string path = PathFrom(key, extension);
                if (File.Exists(path)) File.Delete(path);
                return UniTask.FromResult(EnumResult<TaskError>.SuccessResult());
            }
            catch (Exception e) { return UniTask.FromResult(EnumResult<TaskError>.ErrorResult(TaskError.UnexpectedException, e.Message ?? string.Empty)); }
        }

        private string PathFrom(string key, string extension)
        {
            string path = HashNamings.HashNameFrom(key, extension);
            path = Path.Combine(dirPath, path);
            return path;
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

        public async UniTask<EnumResult<TaskError>> PutAsync(string key, string extension, T data, CancellationToken token)
        {
            using IMemoryOwner<byte> serializedData = await serializer.Serialize(data, token);
            return await diskCache.PutAsync(key, extension, serializedData.Memory, token);
        }

        public async UniTask<EnumResult<Option<T>, TaskError>> ContentAsync(string key, string extension, CancellationToken token)
        {
            var result = await diskCache.ContentAsync(key, extension, token);

            if (result.Success == false)
                return EnumResult<Option<T>, TaskError>.ErrorResult(result.Error!.Value.State, result.Error.Value.Message!);

            IMemoryOwner<byte>? data = result.Value;

            if (data == null)
                return EnumResult<Option<T>, TaskError>.SuccessResult(Option<T>.None);

            T deserializedValue = await serializer.Deserialize(data, token);
            return EnumResult<Option<T>, TaskError>.SuccessResult(Option<T>.Some(deserializedValue));
        }

        public UniTask<EnumResult<TaskError>> RemoveAsync(string key, string extension, CancellationToken token) =>
            diskCache.RemoveAsync(key, extension, token);
    }
}
