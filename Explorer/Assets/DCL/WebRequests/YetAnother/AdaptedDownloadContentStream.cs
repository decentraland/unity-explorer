using Best.HTTP.Shared.Extensions;
using Best.HTTP.Shared.PlatformSupport.Memory;
using Best.HTTP.Shared.Streams;
using Cysharp.Threading.Tasks;
using System;
using System.IO;
using System.Threading;

namespace DCL.WebRequests
{
    public readonly struct AdaptedDownloadContentStream
    {
        private const int BUFFER_SIZE = 16 * 1024; // 16 KB

        // will be disposed of with the response message
        private readonly Stream httpContentStream;

        public AdaptedDownloadContentStream(Stream httpContentStream)
        {
            this.httpContentStream = httpContentStream;
        }

        public async UniTask<(BufferSegment segment, bool finished)> TryTakeNextAsync(CancellationToken ct)
        {
            byte[] buffer = BufferPool.Get(BUFFER_SIZE, true);
            ;

            int bytesRead = await httpContentStream.ReadAsync(buffer, 0, BUFFER_SIZE, ct);

            if (bytesRead <= 0)
            {
                // No more data to read
                BufferPool.Release(buffer);
                return (default(BufferSegment), true);
            }

            BufferSegment segment = buffer.AsBuffer(bytesRead);
            return (segment, false);
        }

        public async UniTask<BufferSegmentStream> GetCompleteContentStreamAsync(CancellationToken cancellationToken)
        {
            var contentStream = new BufferSegmentStream();

            int bytesRead;

            byte[] buffer = BufferPool.Get(BUFFER_SIZE, true);
            ;

            try
            {
                while ((bytesRead = await httpContentStream.ReadAsync(buffer, 0, BUFFER_SIZE, cancellationToken)) > 0)
                {
                    BufferSegment segment = buffer.AsBuffer(bytesRead);
                    contentStream.Write(segment);

                    // Renew the buffer
                    buffer = BufferPool.Get(BUFFER_SIZE, true);
                }
            }
            finally
            {
                // Release the unused buffer back to the pool
                BufferPool.Release(buffer);
            }

            return contentStream;
        }
    }
}
