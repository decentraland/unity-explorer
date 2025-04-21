using Best.HTTP;
using Best.HTTP.Response;
using Best.HTTP.Shared.PlatformSupport.Memory;
using Best.HTTP.Shared.PlatformSupport.Text;
using Best.HTTP.Shared.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DCL.WebRequests.HTTP2
{
    public class Http2WebRequest : WebRequestBase, IWebRequest
    {
        internal readonly HTTPRequest httpRequest;

        public DateTime CreationTime { get; }

        public ulong DownloadedBytes { get; private set; }

        public ulong UploadedBytes { get; private set; }

        public IWebRequestResponse Response { get; }

        public bool IsTimedOut => httpRequest.State is HTTPRequestStates.TimedOut or HTTPRequestStates.ConnectionTimedOut;

        public bool IsAborted => httpRequest.State == HTTPRequestStates.Aborted || !Response.Received;

        public bool Redirected => httpRequest.RedirectSettings.IsRedirected && httpRequest.RedirectSettings.RedirectCount > 0;

        object IWebRequest.nativeRequest => httpRequest;

        public event Action<IWebRequest>? OnDownloadStarted;

        public Http2WebRequest(HTTPRequest httpRequest, ITypedWebRequest createdFrom) : base(createdFrom)
        {
            this.httpRequest = httpRequest;
            Response = new Http2Response(httpRequest);
            CreationTime = DateTime.Now;

            httpRequest.DownloadSettings.OnDownloadStarted += OnStarted;
            httpRequest.DownloadSettings.OnDownloadProgress += OnDownloadProgress;
            httpRequest.UploadSettings.OnUploadProgress += OnUploadProgress;
        }

        private void OnDownloadProgress(HTTPRequest req, long progress, long length)
        {
            // If there is no content size, it won't be reported
            DownloadedBytes = (ulong)Math.Max(progress, length);
        }

        private void OnUploadProgress(HTTPRequest req, long progress, long length)
        {
            UploadedBytes = (ulong)progress;
        }

        private void OnStarted(HTTPRequest req, HTTPResponse resp, DownloadContentStream stream)
        {
            OnDownloadStarted?.Invoke(this);
        }

        public void Abort()
        {
            httpRequest.Abort();
        }

        public void SetRequestHeader(string name, string value)
        {
            httpRequest.SetHeader(name, value);
        }

        public void SetTimeout(int timeout)
        {
            // If zero is passed don't override timeout (it will be 20 seconds for establishing a connection, and no timeout for completing)
            if (timeout > 0)
                httpRequest.TimeoutSettings.Timeout = TimeSpan.FromSeconds(timeout);
        }

        protected override void OnDispose()
        {
            if (httpRequest.Response?.DownStream?.IsDetached == true)
                httpRequest.Response.DownStream.Dispose();
        }

        internal class Http2Response : IWebRequestResponse
        {
            private readonly HTTPRequest request;

            public bool Received => request.Response != null;

            public string Text
            {
                get
                {
                    if (request.Response == null) return string.Empty;

                    using DownloadContentStream? stream = request.Response.DownStream;

                    // Create a string from the stream

                    // Until we read the whole buffer we don't know the size of the string (char encoding is variable)
                    // We can't read the down stream multiple times as it dequeues the chunks internally
                    // We can't read by chunks straight-away either as trailing bytes may belong to the next character

                    long bytesLength = stream.Length;

                    // So we need a StringBuilder :-(
                    // This length is just a hint, it's not the actual length of the string
                    StringBuilder? stringBuilder = StringBuilderPool.Get((int)bytesLength / 2);

                    try
                    {
                        // Warning: it creates a new instance of decoder
                        Decoder decoder = Encoding.UTF8.GetDecoder();

                        while (TryReadSegment(decoder)) ;

                        return stringBuilder.ToString();
                    }
                    catch (DecoderFallbackException) { return string.Empty; }
                    finally { StringBuilderPool.Release(stringBuilder); }

                    bool TryReadSegment(Decoder decoder)
                    {
                        if (stream.TryTake(out BufferSegment bufferSegment))
                        {
                            // Check if we can actually allocate a segment on stack to avoid heap allocations
                            Span<char> charsBuffer = stackalloc char[bufferSegment.Count];

                            // Setting flush to `false` will preserve trailing bytes of the next character
                            int charsDecoded = decoder.GetChars(bufferSegment.AsSpan(), charsBuffer, false);

                            stringBuilder.Append(charsBuffer[..charsDecoded]);

                            BufferPool.Release(bufferSegment);
                            return true;
                        }

                        return false;
                    }
                }
            }

            public string Error => IsSuccess ? string.Empty : request.Response?.Message ?? string.Empty;

            public byte[] Data
            {
                get
                {
                    // The following will not work as it checks the response itself is disposed of (but should rely on the stream instead)
                    // request.Response?.Data ?? Array.Empty<byte>();
                    DownloadContentStream? downStream = request.Response.DownStream;

                    var data = new byte[downStream.Length];
                    downStream.Read(data, 0, data.Length);
                    return data;
                }
            }

            public int StatusCode
            {
                get
                {
                    // If File Not Found it's not reported as 404
                    // Adapt it

                    if (request.Exception is FileNotFoundException or DirectoryNotFoundException)
                        return HTTPStatusCodes.NotFound;

                    return request.Response?.StatusCode ?? 0;
                }
            }

            public bool IsSuccess => request.State == HTTPRequestStates.Finished;

            public ulong DataLength => (ulong)(request.Response?.DownStream?.Length ?? 0);

            internal Http2Response(HTTPRequest request)
            {
                this.request = request;
            }

            public Stream GetCompleteStream()
            {
                using DownloadContentStream downStream = request.Response.DownStream;

                var bufferStream = new BufferSegmentStream();

                while (downStream.TryTake(out BufferSegment segment))
                    bufferStream.Write(segment);

                return bufferStream;
            }

            public string? GetHeader(string headerName) =>
                request.Response.GetFirstHeaderValue(headerName);

            public Dictionary<string, string>? FlattenHeaders() =>
                request.Response?.Headers.FlattenHeaders();
        }
    }
}
