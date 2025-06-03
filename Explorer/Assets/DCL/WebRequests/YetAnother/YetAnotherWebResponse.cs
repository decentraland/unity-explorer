using Best.HTTP.Shared.PlatformSupport.Memory;
using Best.HTTP.Shared.PlatformSupport.Text;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace DCL.WebRequests
{
    public class YetAnotherWebResponse : IWebRequestResponse, IDisposable
    {
        internal readonly HttpResponseMessage response;

        internal AdaptedDownloadContentStream downStream;

        internal YetAnotherWebResponse(HttpResponseMessage response, AdaptedDownloadContentStream downStream)
        {
            this.response = response;
            this.downStream = downStream;
            Error = string.Empty;
        }

        public bool Received => true;

        public async UniTask<byte[]> GetDataAsync(CancellationToken ct)
        {
            Stream? stream = await GetCompleteStreamAsync(ct);

            using (stream)
            {
                var buffer = new byte[stream.Length];
                await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                return buffer;
            }
        }

        public async UniTask<string> GetTextAsync(CancellationToken ct)
        {
            StringBuilder? stringBuilder = StringBuilderPool.Get(100);

            // Warning: it creates a new instance of decoder
            Decoder decoder = Encoding.UTF8.GetDecoder();

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    bool finished;
                    BufferSegment segment;
                    (segment, finished) = await downStream.TryTakeNextAsync(ct);

                    if (finished)
                        return stringBuilder.ToString();

                    using AutoReleaseBuffer _ = segment.AsAutoRelease();
                    AppendChunk(decoder, segment);
                }
            }
            catch (DecoderFallbackException) { return string.Empty; }
            finally { StringBuilderPool.Release(stringBuilder); }

            return string.Empty;

            void AppendChunk(Decoder decoder, BufferSegment bufferSegment)
            {
                // Check if we can actually allocate a segment on stack to avoid heap allocations
                Span<char> charsBuffer = stackalloc char[bufferSegment.Count];

                // Setting flush to `false` will preserve trailing bytes of the next character
                int charsDecoded = decoder.GetChars(bufferSegment.AsSpan(), charsBuffer, false);

                stringBuilder.Append(charsBuffer[..charsDecoded]);
            }
        }

        /// <summary>
        ///     Error must be set from the exception
        /// </summary>
        public string Error { get; internal set; }

        public int StatusCode => (int)response.StatusCode;

        public bool IsSuccess => response.IsSuccessStatusCode;

        public ulong DataLength => (ulong)(response.Content.Headers.ContentLength ?? 0L);

        public async UniTask<Stream> GetCompleteStreamAsync(CancellationToken ct) =>
            await downStream.GetCompleteContentStreamAsync(ct);

        public string? GetHeader(string headerName) =>
            response.Headers.TryGetValues(headerName, out IEnumerable<string>? values) ? values.First() :
            response.Content.Headers.TryGetValues(headerName, out values) ? values.First() : null;

        public Dictionary<string, string>? FlattenHeaders()
        {
            return response.Headers.Concat(response.Content.Headers)
                           .ToDictionary(
                                kvp => kvp.Key,
                                kvp => string.Join(", ", kvp.Value),
                                StringComparer.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
            response.Dispose();
        }
    }
}
