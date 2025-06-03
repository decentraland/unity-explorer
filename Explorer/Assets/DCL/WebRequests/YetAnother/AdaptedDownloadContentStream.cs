using Best.HTTP.Shared.Extensions;
using Best.HTTP.Shared.PlatformSupport.Memory;
using Best.HTTP.Shared.Streams;
using Cysharp.Threading.Tasks;
using System.IO;
using System.Threading;

namespace DCL.WebRequests
{
    internal struct AdaptedDownloadContentStream
    {
        private const int BUFFER_SIZE = 16 * 1024; // 16 KB

        // will be disposed of with the response message
        private readonly Stream httpContentStream;

        /// <summary>
        ///     Downloaded bytes increment when the content stream is read.
        /// </summary>
        public ulong DownloadedBytes { get; private set; }

        public AdaptedDownloadContentStream(Stream httpContentStream)
        {
            this.httpContentStream = httpContentStream;
            DownloadedBytes = 0;
        }

        public async UniTask<(BufferSegment segment, bool finished)> TryTakeNextAsync(CancellationToken ct)
        {
            byte[] buffer = BufferPool.Get(BUFFER_SIZE, true);

            int bytesRead = await httpContentStream.ReadAsync(buffer, 0, BUFFER_SIZE, ct);

            if (bytesRead <= 0)
            {
                // No more data to read
                BufferPool.Release(buffer);
                return (default(BufferSegment), true);
            }

            DownloadedBytes += (ulong)bytesRead;

            BufferSegment segment = buffer.AsBuffer(bytesRead);
            return (segment, false);
        }

        public async UniTask<BufferSegmentStream> GetCompleteContentStreamAsync(CancellationToken cancellationToken)
        {
            var contentStream = new BufferSegmentStream();

            while (!cancellationToken.IsCancellationRequested)
            {
                (BufferSegment segment, bool finished) = await TryTakeNextAsync(cancellationToken);

                if (finished)
                    break;

                contentStream.Write(segment);
            }

            return contentStream;
        }
    }
}
