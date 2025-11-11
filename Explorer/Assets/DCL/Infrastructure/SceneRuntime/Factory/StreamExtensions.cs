using Cysharp.Threading.Tasks;
using System.IO;

namespace SceneRuntime.Factory
{
    public static class StreamExtensions
    {
        public static async UniTask ReadReliablyAsync(this Stream stream, byte[] buffer, int offset,
            int count)
        {
            while (buffer.Length > 0)
            {
                int read = await stream.ReadAsync(buffer, offset, count);

                if (read <= 0)
                    throw new EndOfStreamException("Read zero bytes");

                offset += read;
                count -= read;
            }
        }
    }
}
