using System;
using System.Net.Http;

namespace DCL.WebRequests
{
    public class YetAnotherWebRequest : WebRequestBase, IWebRequest
    {
        internal readonly HttpRequestMessage request;
        internal YetAnotherWebResponse? response { get; private set; }

        public YetAnotherWebRequest(HttpRequestMessage request, ITypedWebRequest createdFrom) : base(createdFrom)
        {
            CreationTime = DateTime.Now;

            this.request = request;
        }

        public void SetTimeout(int timeout)
        {
            // Not supported per request
        }

        public void SetRequestHeader(string name, string value)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        internal void SetResponse(HttpResponseMessage response, AdaptedDownloadContentStream responseContentStream)
        {
            this.response = new YetAnotherWebResponse(response, responseContentStream);

            Response = this.response;
            successfullyExecutedByController = true;

            OnDownloadStarted?.Invoke(this);
        }

        public IWebRequestResponse Response { get; private set; } = NotReceivedResponse.INSTANCE;

        public bool Redirected => response?.response.RequestMessage?.RequestUri != request.RequestUri;

        /// <summary>
        ///     Must be manually set
        /// </summary>
        public bool IsTimedOut { get; internal set; }

        // TODO investigate how to detect
        public bool IsAborted => !Response.Received;

        object IWebRequest.nativeRequest => request;

        public event Action<IWebRequest>? OnDownloadStarted;

        /// <summary>
        ///     Implementation is not needed as <see cref="YetAnotherWebRequestController" /> will stop the execution on headers received
        ///     and if no content is needed, the request will be simply disposed of
        /// </summary>
        public void Abort()
        {
        }

        public DateTime CreationTime { get; }

        public ulong DownloadedBytes => response?.downStream.DownloadedBytes ?? 0;

        // TODO
        public ulong UploadedBytes => 0;

        protected override void OnDispose()
        {
            // Dispose of the stream if it was created

            request.Dispose();
            response?.Dispose();
        }
    }
}
