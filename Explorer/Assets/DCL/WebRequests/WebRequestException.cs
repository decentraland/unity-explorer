using Best.HTTP;
using Best.HTTP.Response;
using Cysharp.Threading.Tasks;
using Nethereum.Contracts;
using System;
using System.Collections.Generic;
using System.IO;

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
        public Uri Url { get; }
        public int ResponseCode { get; }

        /// <summary>
        ///     <inheritdoc cref="IWebRequestResponse.Received" />
        /// </summary>
        public bool ResponseReceived { get; }
        public bool IsTimedOut { get; }
        public bool IsAborted { get; }
        public bool IsServerError { get; }

        public string Text { get; protected set; } = string.Empty;

        public virtual Dictionary<string, string>? ResponseHeaders => null;

        /// <summary>
        ///     Copies values from <see cref="webRequest" />. It's mandatory to not store <see cref="webRequest" /> as it will be disposed after the exception is created
        /// </summary>
        /// <param name="webRequest"></param>
        /// <param name="nativeException"></param>
        protected WebRequestException(IWebRequest webRequest, Exception nativeException) : base($"{nameof(WebRequestException)} was thrown.", nativeException)
        {
            Error = webRequest.Response.Error ?? string.Empty;
            Url = webRequest.Url;
            ResponseCode = webRequest.Response.StatusCode;
            ResponseReceived = webRequest.Response.Received;
            IsTimedOut = webRequest.IsTimedOut;
            IsAborted = webRequest.IsAborted;
            IsServerError = webRequest.IsServerError();
        }

        internal WebRequestException(Uri fileUrl, Exception fileException) : base($"{nameof(WebRequestException)} was thrown.", fileException)
        {
            Url = fileUrl;
            Error = fileException.Message;
            ResponseReceived = true;
            IsTimedOut = false;
            IsAborted = false;
            IsServerError = false;

            switch (fileException)
            {
                case FileNotFoundException or DirectoryNotFoundException:
                    ResponseCode = HTTPStatusCodes.NotFound; break;
                case UnauthorizedAccessException:
                    ResponseCode = HTTPStatusCodes.Unauthorized; break;
                case IOException:
                    ResponseCode = HTTPStatusCodes.InternalServerError;
                    IsServerError = true;
                    break;
                default:
                    ResponseCode = HTTPStatusCodes.BadRequest; break;
            }
        }
    }
}
