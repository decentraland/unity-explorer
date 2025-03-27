using Best.HTTP;
using System.Collections.Generic;

namespace DCL.WebRequests.HTTP2
{
    public class Http2WebRequestException : WebRequestException
    {
        private readonly Dictionary<string, List<string>>? originalHeaders;

        public Http2WebRequestException(Http2WebRequest webRequest, AsyncHTTPException originalException) : base(webRequest, originalException)
        {
            // avoid allocation of headers if they are not requested
            // Original headers are already allocated
            originalHeaders = webRequest.httpRequest.Response?.Headers;

            Text = originalException.Content;
        }

        public override Dictionary<string, string>? ResponseHeaders => originalHeaders.FlattenHeaders();
    }
}
