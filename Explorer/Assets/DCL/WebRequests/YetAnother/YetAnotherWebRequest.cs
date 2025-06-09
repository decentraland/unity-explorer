using Best.HTTP;
using System;
using System.Net.Http;

namespace DCL.WebRequests
{
    public class YetAnotherWebRequest : WebRequestBase, IWebRequest
    {
        internal readonly HTTPMethods Method;
        internal HttpRequestMessage request;

        public IWebRequestResponse Response { get; private set; } = NotReceivedResponse.INSTANCE;

        /// <summary>
        ///     Redirect is resolved manually
        /// </summary>
        public bool Redirected { get; private set; }

        /// <summary>
        ///     Must be manually set
        /// </summary>
        public bool IsTimedOut { get; internal set; }

        // HttpClient does not support aborting
        public bool IsAborted => !Response.Received;

        public DateTime CreationTime { get; }

        public ulong DownloadedBytes => response?.downStream.DownloadedBytes ?? 0;

        public ulong UploadedBytes { get; internal set; }

        internal YetAnotherWebResponse? response { get; private set; }

        object IWebRequest.nativeRequest => request;

        public event Action<IWebRequest>? OnDownloadStarted;

        public YetAnotherWebRequest(HttpRequestMessage request, ITypedWebRequest createdFrom) : base(createdFrom)
        {
            CreationTime = DateTime.Now;
            Method = Enum.Parse<HTTPMethods>(request.Method.Method, true);
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

        internal void SetRedirected(HttpRequestMessage requestMessage)
        {
            request = requestMessage;
            Redirected = true;
        }

        internal YetAnotherWebResponse SetResponse(HttpResponseMessage response, WebRequestHeaders headers, YetAnotherDownloadContentStream responseContentStream)
        {
            this.response = new YetAnotherWebResponse(response, headers, responseContentStream);

            Response = this.response;

            OnDownloadStarted?.Invoke(this);
            return this.response;
        }

        /// <summary>
        ///     Implementation is not needed as <see cref="YetAnotherWebRequestController" /> will stop the execution on headers received
        ///     and if no content is needed, the request will be simply disposed of
        /// </summary>
        public void Abort() { }

        protected override void OnDispose()
        {
            // Dispose of the stream if it was created

            request.Dispose();
            response?.Dispose();
        }
    }
}
