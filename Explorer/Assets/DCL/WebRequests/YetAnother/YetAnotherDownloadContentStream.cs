using Best.HTTP.Caching;
using Best.HTTP.Shared.Extensions;
using Best.HTTP.Shared.PlatformSupport.Memory;
using Best.HTTP.Shared.Streams;
using Cysharp.Threading.Tasks;
using System.IO;
using System.Net.Http;
using System.Threading;
using Utility.Multithreading;

namespace DCL.WebRequests
{
    /// <summary>
    ///     <para>
    ///         Adapts the stream received from <see cref="HttpClient" /> to the <see cref="BufferSegment" /> format
    ///     </para>
    ///     <para>
    ///         Uses <see cref="HTTPCache" /> to cache the content stream while it's being retrieved
    ///     </para>
    /// </summary>
    internal struct YetAnotherDownloadContentStream
    {
        private const int BUFFER_SIZE = 16 * 1024; // 16 KB

        // will be disposed of with the response message
        private readonly Stream httpContentStream;

        public YetAnotherDownloadContentStream(Stream httpContentStream)
        {
            this.httpContentStream = httpContentStream;
            DownloadedBytes = 0;
        }

        /// <summary>
        ///     Downloaded bytes increment when the content stream is read.
        /// </summary>
        public ulong DownloadedBytes { get; private set; }

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
            await using ExecuteOnThreadPoolScope _ = await ExecuteOnThreadPoolScope.NewScopeAsync();
            var contentStream = new BufferSegmentStream();

            while (!cancellationToken.IsCancellationRequested)
            {
                // The inner function doesn't preserve the synchronization context so it returns to the thread pool
                (BufferSegment segment, bool finished) = await TryTakeNextAsync(cancellationToken);

                if (finished)
                    break;

                contentStream.Write(segment);
            }

            return contentStream;
        }
    }
}
