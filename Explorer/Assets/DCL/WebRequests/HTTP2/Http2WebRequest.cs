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
        private readonly HTTPRequest httpRequest;

        public string Url => httpRequest.Uri.OriginalString;

        public IWebRequest.IResponse Response { get; }

        object IWebRequest.nativeRequest => httpRequest;

        public bool Redirected => httpRequest.RedirectSettings.IsRedirected && httpRequest.RedirectSettings.RedirectCount > 0;

        public Http2WebRequest(HTTPRequest httpRequest)
        {
            this.httpRequest = httpRequest;
            Response = new Http2Response(httpRequest);
        }

        public void Dispose()
        {
            // TODO
        }

        public void SetRequestHeader(string name, string value)
        {
            httpRequest.SetHeader(name, value);
        }

        public void SetTimeout(int timeout)
        {
            httpRequest.TimeoutSettings.Timeout = TimeSpan.FromSeconds(timeout);
        }

        internal class Http2Response : IWebRequest.IResponse
        {
            private readonly HTTPRequest request;

            // TODO support graceful string.Empty
            public string Text
            {
                get
                {
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

            public byte[] Data => request.Response.Data;

            public int StatusCode => request.Response.StatusCode;

            public bool IsSuccess => request.State == HTTPRequestStates.Finished;
            public bool IsAborted => request.State == HTTPRequestStates.Aborted;
            public bool IsTimedOut => request.State is HTTPRequestStates.TimedOut or HTTPRequestStates.Error;

            internal Http2Response(HTTPRequest request)
            {
                this.request = request;
            }

            public string GetHeader(string headerName) =>
                request.Response.GetFirstHeaderValue(headerName);

            public Dictionary<string, string> FlattenHeaders() =>
                request.Response.Headers
        }
    }
}
