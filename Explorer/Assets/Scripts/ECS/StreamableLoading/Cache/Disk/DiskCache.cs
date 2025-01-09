using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Buffers;
using System.IO;
using System.Threading;
using UnityEngine;
using Utility.Types;

namespace ECS.StreamableLoading.Cache.Disk
{
    public class DiskCache : IDiskCache
    {
        private readonly string dirPath;

        public DiskCache(string dirPath)
        {
            dirPath = Path.Combine(Application.persistentDataPath!, dirPath);
            this.dirPath = dirPath;
            if (Directory.Exists(dirPath) == false) Directory.CreateDirectory(dirPath);
            ReportHub.Log(ReportCategory.DEBUG, $"DiskCache: use directory at {dirPath}");
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

        public async UniTask<EnumResult<SlicedOwnedMemory<byte>?, TaskError>> ContentAsync(string key, string extension, CancellationToken token)
        {
            try
            {
                string path = PathFrom(key, extension);

                if (File.Exists(path) == false)
                    return EnumResult<SlicedOwnedMemory<byte>?, TaskError>.SuccessResult(null);

                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                var data = new SlicedOwnedMemory<byte>(MemoryPool<byte>.Shared!.Rent((int)stream.Length)!, (int)stream.Length);

                int _ = await stream.ReadAsync(data.Memory, token);
                return EnumResult<SlicedOwnedMemory<byte>?, TaskError>.SuccessResult(data);
            }
            catch (TimeoutException) { return EnumResult<SlicedOwnedMemory<byte>?, TaskError>.ErrorResult(TaskError.Timeout); }
            catch (OperationCanceledException) { return EnumResult<SlicedOwnedMemory<byte>?, TaskError>.ErrorResult(TaskError.Cancelled); }
            catch (Exception e) { return EnumResult<SlicedOwnedMemory<byte>?, TaskError>.ErrorResult(TaskError.UnexpectedException, e.Message ?? string.Empty); }
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
            using SlicedOwnedMemory<byte> serializedData = await serializer.Serialize(data, token);
            return await diskCache.PutAsync(key, extension, serializedData.Memory, token);
        }

        public async UniTask<EnumResult<Option<T>, TaskError>> ContentAsync(string key, string extension, CancellationToken token)
        {
            var result = await diskCache.ContentAsync(key, extension, token);

            if (result.Success == false)
                return EnumResult<Option<T>, TaskError>.ErrorResult(result.Error!.Value.State, result.Error.Value.Message!);

            SlicedOwnedMemory<byte>? data = result.Value;

            if (data == null)
                return EnumResult<Option<T>, TaskError>.SuccessResult(Option<T>.None);

            T deserializedValue = await serializer.Deserialize(data.Value, token);
            return EnumResult<Option<T>, TaskError>.SuccessResult(Option<T>.Some(deserializedValue));
        }

        public UniTask<EnumResult<TaskError>> RemoveAsync(string key, string extension, CancellationToken token) =>
            diskCache.RemoveAsync(key, extension, token);
    }
}
