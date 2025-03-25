using Best.HTTP;
using Cysharp.Threading.Tasks;
using Nethereum.Contracts;
using System;
using System.Collections.Generic;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Common web request exception which is abstracted from <see cref="AsyncHTTPException" /> and <see cref="UnityWebRequestException" />
    /// </summary>
    public class WebRequestException : Exception
    {
        /// <summary>
        ///     Will be empty for HTTP2 requests
        /// </summary>
        public string Error { get; }

        public string Url { get; }

        public int ResponseCode { get; }

        /// <summary>
        ///     Won't be available if the request has been already disposed
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Won't be available if the request has been already disposed
        /// </summary>
        public Dictionary<string, string> ResponseHeaders { get; }
    }
}
