using Cysharp.Threading.Tasks;
using System;
using System.IO;
using System.Threading;
using Utility.Types;

namespace DCL.Caches.Core.Disk
{
    public class DiskCache : IDiskCache
    {
        private readonly string dirPath;

        public DiskCache(string dirPath)
        {
            this.dirPath = dirPath;
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

        public async UniTask<EnumResult<byte[]?, TaskError>> ContentAsync(string key, string extension, CancellationToken token)
        {
            try
            {
                string path = PathFrom(key, extension);

                if (File.Exists(path) == false)
                    return EnumResult<byte[]?, TaskError>.SuccessResult(null);

                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                var data = new byte[stream.Length];

                int _ = await stream.ReadAsync(data, token);
                return EnumResult<byte[]?, TaskError>.SuccessResult(data);
            }
            catch (TimeoutException) { return EnumResult<byte[]?, TaskError>.ErrorResult(TaskError.Timeout); }
            catch (OperationCanceledException) { return EnumResult<byte[]?, TaskError>.ErrorResult(TaskError.Cancelled); }
            catch (Exception e) { return EnumResult<byte[]?, TaskError>.ErrorResult(TaskError.UnexpectedException, e.Message ?? string.Empty); }
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
}
