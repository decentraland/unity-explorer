using Cysharp.Threading.Tasks;
using DCL.Utility.Types;
using System;
using System.IO;

namespace SceneRuntime.Factory
{
    public static class StreamExtensions
    {
        public static async UniTask<Result> ReadReliablyAsync(
            this Stream stream, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                int read = await stream.ReadAsync(buffer, offset, count);

                if (read <= 0)
                    return Result.ErrorResult("Read zero bytes");

                offset += read;
                count -= read;
            }

            return Result.SuccessResult();
        }

        public static async UniTask<Result> ReadReliablyAsync(
            this Stream stream, Memory<byte> buffer)
        {
            while (buffer.Length > 0)
            {
                int read = await stream.ReadAsync(buffer);

                if (read <= 0)
                    return Result.ErrorResult("Read zero bytes");

                buffer = buffer.Slice(read);
            }

            return Result.SuccessResult();
        }
    }
}
