using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Thirdweb;
using UnityEngine.Networking;

namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     Adapter that implements Thirdweb SDK's <see cref="IThirdwebHttpClient"/> using the project's
    ///     <see cref="IWebRequestController"/>, ensuring all HTTP traffic goes through our
    ///     allocation-optimized, budgeted web request infrastructure (analytics, Sentry, Chrome DevTools, etc.).
    /// </summary>
    internal class DclThirdwebHttpClient : IThirdwebHttpClient
    {
        /// <summary>
        ///     Shared controller reference, set once by the primary constructor and reused
        ///     by reflection-created clones (Thirdweb SDK's <c>Utils.ReconstructHttpClient</c>
        ///     instantiates clones via a parameterless constructor found through reflection).
        /// </summary>
        private static IWebRequestController? sharedWebRequestController;

        private readonly IWebRequestController webRequestController;

        public Dictionary<string, string> Headers { get; private set; } = new ();

        /// <summary>
        ///     Primary constructor — called explicitly when wiring up the Thirdweb client.
        /// </summary>
        internal DclThirdwebHttpClient(IWebRequestController webRequestController)
        {
            sharedWebRequestController = webRequestController;
            this.webRequestController = webRequestController;
        }

        /// <summary>
        ///     Parameterless constructor required by Thirdweb SDK internals.
        ///     <c>Thirdweb.Utils.ReconstructHttpClient</c> clones the HTTP client via reflection
        ///     using <c>GetConstructor(Type.EmptyTypes).Invoke(null)</c>.
        /// </summary>
        [UsedImplicitly]
        public DclThirdwebHttpClient()
        {
            this.webRequestController = sharedWebRequestController
                ?? throw new InvalidOperationException(
                    $"{nameof(DclThirdwebHttpClient)} must first be created with {nameof(IWebRequestController)} before parameterless construction.");
        }

        public void Dispose() { }

        public void SetHeaders(Dictionary<string, string> headers) =>
            Headers = new Dictionary<string, string>(headers);

        public void ClearHeaders() =>
            Headers.Clear();

        public void AddHeader(string key, string value) =>
            Headers[key] = value;

        public void RemoveHeader(string key) =>
            Headers.Remove(key);

        public Task<ThirdwebHttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken = default) =>
            SendGetInternalAsync(requestUri, cancellationToken).AsTask();

        public Task<ThirdwebHttpResponseMessage> PostAsync(string requestUri, HttpContent content, CancellationToken cancellationToken = default) =>
            SendPostInternalAsync(requestUri, content, cancellationToken).AsTask();

        public Task<ThirdwebHttpResponseMessage> PutAsync(string requestUri, HttpContent content, CancellationToken cancellationToken = default) =>
            SendPutInternalAsync(requestUri, content, cancellationToken).AsTask();

        public Task<ThirdwebHttpResponseMessage> DeleteAsync(string requestUri, CancellationToken cancellationToken = default) =>
            SendDeleteInternalAsync(requestUri, cancellationToken).AsTask();

        private async UniTask<ThirdwebHttpResponseMessage> SendGetInternalAsync(string requestUri, CancellationToken ct)
        {
            try
            {
                return await webRequestController
                    .SendAsync<GenericGetRequest, GenericGetArguments, ThirdwebResponseOp<GenericGetRequest>, ThirdwebHttpResponseMessage>(
                        CommonArgumentsFor(requestUri),
                        default(GenericGetArguments),
                        new ThirdwebResponseOp<GenericGetRequest>(),
                        ct,
                        ReportCategory.AUTHENTICATION,
                        headersInfo: CreateHeadersInfo(),
                        suppressErrors: true
                    );
            }
            catch (UnityWebRequestException ex) { return WrapErrorResponse(ex); }
        }

        private async UniTask<ThirdwebHttpResponseMessage> SendPostInternalAsync(string requestUri, HttpContent content, CancellationToken ct)
        {
            string postData = await content.ReadAsStringAsync();
            string contentType = content.Headers.ContentType?.ToString() ?? GenericPostArguments.JSON;

            try
            {
                return await webRequestController
                    .SendAsync<GenericPostRequest, GenericPostArguments, ThirdwebResponseOp<GenericPostRequest>, ThirdwebHttpResponseMessage>(
                        CommonArgumentsFor(requestUri),
                        GenericPostArguments.Create(postData, contentType),
                        new ThirdwebResponseOp<GenericPostRequest>(),
                        ct,
                        ReportCategory.AUTHENTICATION,
                        headersInfo: CreateHeadersInfo(),
                        suppressErrors: true
                    );
            }
            catch (UnityWebRequestException ex) { return WrapErrorResponse(ex); }
        }

        private async UniTask<ThirdwebHttpResponseMessage> SendPutInternalAsync(string requestUri, HttpContent content, CancellationToken ct)
        {
            string postData = await content.ReadAsStringAsync();
            string contentType = content.Headers.ContentType?.ToString() ?? GenericPostArguments.JSON;

            try
            {
                return await webRequestController
                    .SendAsync<GenericPutRequest, GenericPostArguments, ThirdwebResponseOp<GenericPutRequest>, ThirdwebHttpResponseMessage>(
                        CommonArgumentsFor(requestUri),
                        GenericPostArguments.Create(postData, contentType),
                        new ThirdwebResponseOp<GenericPutRequest>(),
                        ct,
                        ReportCategory.AUTHENTICATION,
                        headersInfo: CreateHeadersInfo(),
                        suppressErrors: true
                    );
            }
            catch (UnityWebRequestException ex) { return WrapErrorResponse(ex); }
        }

        private async UniTask<ThirdwebHttpResponseMessage> SendDeleteInternalAsync(string requestUri, CancellationToken ct)
        {
            try
            {
                return await webRequestController
                    .SendAsync<GenericDeleteRequest, GenericPostArguments, ThirdwebResponseOp<GenericDeleteRequest>, ThirdwebHttpResponseMessage>(
                        CommonArgumentsFor(requestUri),
                        GenericPostArguments.Empty,
                        new ThirdwebResponseOp<GenericDeleteRequest>(),
                        ct,
                        ReportCategory.AUTHENTICATION,
                        headersInfo: CreateHeadersInfo(),
                        suppressErrors: true
                    );
            }
            catch (UnityWebRequestException ex) { return WrapErrorResponse(ex); }
        }

        private static CommonArguments CommonArgumentsFor(string requestUri) =>
            new (URLAddress.FromString(requestUri), RetryPolicy.NONE);

        private WebRequestHeadersInfo CreateHeadersInfo() =>
            new (Headers);

        /// <summary>
        ///     Wraps a non-2xx <see cref="UnityWebRequestException"/> into <see cref="ThirdwebHttpResponseMessage"/>
        ///     instead of throwing, since the Thirdweb SDK checks <see cref="ThirdwebHttpResponseMessage.IsSuccessStatusCode"/>
        ///     and handles errors on its own.
        /// </summary>
        private static ThirdwebHttpResponseMessage WrapErrorResponse(UnityWebRequestException ex) =>
            new (
                statusCode: ex.ResponseCode,
                content: new ThirdwebHttpContent(ex.Text ?? string.Empty),
                isSuccessStatusCode: false
            );

        /// <summary>
        ///     Operation that captures raw response bytes and status code on successful requests.
        /// </summary>
        private readonly struct ThirdwebResponseOp<TRequest> : IWebRequestOp<TRequest, ThirdwebHttpResponseMessage>
            where TRequest : struct, ITypedWebRequest
        {
            public UniTask<ThirdwebHttpResponseMessage?> ExecuteAsync(TRequest webRequest, CancellationToken ct)
            {
                var wr = webRequest.UnityWebRequest;
                byte[] data = wr.downloadHandler?.data ?? Array.Empty<byte>();
                long statusCode = wr.responseCode;

                return UniTask.FromResult<ThirdwebHttpResponseMessage?>(
                    new ThirdwebHttpResponseMessage(
                        statusCode: statusCode,
                        content: new ThirdwebHttpContent(data),
                        isSuccessStatusCode: statusCode is >= 200 and < 300
                    )
                );
            }
        }
    }
}
