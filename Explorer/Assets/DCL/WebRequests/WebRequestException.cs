using Best.HTTP;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Common web request exception that is abstracted from <see cref="AsyncHTTPException" /> and <see cref="UnityWebRequestException" />
    /// </summary>
    public class WebRequestException : Exception
    {
        public string Url { get; }
        public int ResponseCode { get; }
        public string Text { get; }
        public Dictionary<string, string> ResponseHeaders { get; }

        /// <summary>
        ///     Will be empty for HTTP2 requests
        /// </summary>
        public string Error { get; }
    }
}
