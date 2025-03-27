using Best.HTTP;
using Best.HTTP.Response;
using Best.HTTP.Shared.PlatformSupport.Memory;
using Best.HTTP.Shared.PlatformSupport.Text;
using System;
using System.Collections.Generic;
using System.Text;

namespace DCL.WebRequests.HTTP2
{
    public class Http2WebRequest : IWebRequest
    {
        internal readonly HTTPRequest httpRequest;

        public string Url => httpRequest.Uri.OriginalString;

        public IWebRequestResponse Response { get; }

        public bool IsTimedOut => httpRequest.State is HTTPRequestStates.TimedOut or HTTPRequestStates.ConnectionTimedOut;

        public bool IsAborted => httpRequest.State == HTTPRequestStates.Aborted || !Response.Received;

        public bool Redirected => httpRequest.RedirectSettings.IsRedirected && httpRequest.RedirectSettings.RedirectCount > 0;

        object IWebRequest.nativeRequest => httpRequest;

        public Http2WebRequest(HTTPRequest httpRequest)
        {
            this.httpRequest = httpRequest;
            Response = new Http2Response(httpRequest);
        }

        public void Dispose()
        {
            // Disposal is done automatically by the framework
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
            httpRequest.TimeoutSettings.Timeout = TimeSpan.FromSeconds(timeout);
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

            public string? Error => IsSuccess ? null : request.Response.Message;

            public byte[] Data => request.Response?.Data ?? Array.Empty<byte>();

            public int StatusCode => request.Response.StatusCode;

            public bool IsSuccess => request.State == HTTPRequestStates.Finished;

            internal Http2Response(HTTPRequest request)
            {
                this.request = request;
            }

            public string GetHeader(string headerName) =>
                request.Response.GetFirstHeaderValue(headerName);

            public Dictionary<string, string>? FlattenHeaders() =>
                request.Response?.Headers.FlattenHeaders();
        }
    }
}
