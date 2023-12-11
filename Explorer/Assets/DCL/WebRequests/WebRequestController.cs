using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3Authentication;
using DCL.WebRequests.Analytics;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public class WebRequestController : IWebRequestController
    {
        private static readonly InitializeRequest<GetTextureArguments, GetTextureWebRequest> GET_TEXTURE = GetTextureWebRequest.Initialize;
        private static readonly InitializeRequest<GenericGetArguments, GenericGetRequest> GET_GENERIC = GenericGetRequest.Initialize;
        private static readonly InitializeRequest<GenericPostArguments, GenericPostRequest> POST_GENERIC = GenericPostRequest.Initialize;
        private static readonly InitializeRequest<GenericPutArguments, GenericPutRequest> PUT_GENERIC = GenericPutRequest.Initialize;
        private static readonly InitializeRequest<GenericPatchArguments, GenericPatchRequest> PATCH_GENERIC = GenericPatchRequest.Initialize;

        private readonly IWebRequestsAnalyticsContainer analyticsContainer;
        private readonly IWeb3Authenticator web3Authenticator;

        public WebRequestController(IWebRequestsAnalyticsContainer analyticsContainer,
            IWeb3Authenticator web3Authenticator)
        {
            this.analyticsContainer = analyticsContainer;
            this.web3Authenticator = web3Authenticator;
        }

        public UniTask<GenericGetRequest> GetAsync(
            CommonArguments commonArguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            SendAsync(GET_GENERIC, commonArguments, default(GenericGetArguments), ct, reportCategory, headersInfo, signInfo);

        public UniTask<GenericPostRequest> PostAsync(
            CommonArguments commonArguments,
            GenericPostArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            SendAsync(POST_GENERIC, commonArguments, arguments, ct, reportCategory, headersInfo, signInfo);

        public UniTask<GenericPutRequest> PutAsync(
            CommonArguments commonArguments,
            GenericPutArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            SendAsync(PUT_GENERIC, commonArguments, arguments, ct, reportCategory, headersInfo, signInfo);

        public UniTask<GenericPatchRequest> PatchAsync(
            CommonArguments commonArguments,
            GenericPatchArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            SendAsync(PATCH_GENERIC, commonArguments, arguments, ct, reportCategory, headersInfo, signInfo);

        public UniTask<GetTextureWebRequest> GetTextureAsync(
            CommonArguments commonArguments,
            GetTextureArguments args,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            SendAsync(GET_TEXTURE, commonArguments, args, ct, reportCategory, headersInfo, signInfo);

        private async UniTask<TWebRequest> SendAsync<TWebRequest, TWebRequestArgs>(
            InitializeRequest<TWebRequestArgs, TWebRequest> initializeRequest,
            CommonArguments commonArguments, TWebRequestArgs args,
            CancellationToken ct,
            string reportCategory,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null)
            where TWebRequestArgs: struct
            where TWebRequest: struct, ITypedWebRequest
        {
            int attemptsLeft = Mathf.Max(1, commonArguments.AttemptsCount);

            // ensure disposal of headersInfo
            using WebRequestHeadersInfo _ = headersInfo ?? WebRequestHeadersInfo.EMPTY;

            while (attemptsLeft > 0)
            {
                TWebRequest request = initializeRequest(commonArguments, args);

                UnityWebRequest unityWebRequest = request.UnityWebRequest;

                unityWebRequest.timeout = commonArguments.Timeout;

                if (signInfo.HasValue)
                    SignRequest(signInfo.Value, unityWebRequest);

                if (headersInfo.HasValue)
                    SetHeaders(unityWebRequest, headersInfo.Value);

                if (commonArguments.CustomDownloadHandler != null)
                    unityWebRequest.downloadHandler = commonArguments.CustomDownloadHandler;

                try
                {
                    analyticsContainer.OnRequestStarted(request);

                    await unityWebRequest.SendWebRequest().WithCancellation(ct);

                    // if no exception is thrown Request is successful
                    return request;
                }
                catch (UnityWebRequestException exception)
                {
                    attemptsLeft--;

                    // Make the request no longer usable as all data needed is written into the exception itself
                    exception.UnityWebRequest.Dispose();

                    if (exception.IsIrrecoverableError(attemptsLeft))
                        throw;

                    // Print verbose
                    ReportHub.Log(reportCategory, $"Exception occured on loading {typeof(TWebRequest).Name} from {commonArguments.URL.ToString()}.\n"
                                                  + $"Attempt Left: {attemptsLeft}");
                }
                finally { analyticsContainer.OnRequestFinished(request); }
            }

            throw new Exception($"{nameof(WebRequestController)}: Unexpected code path!");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetHeaders(UnityWebRequest unityWebRequest, WebRequestHeadersInfo headersInfo)
        {
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < headersInfo.Value.Count; i++)
            {
                WebRequestHeader header = headersInfo.Value[i];
                unityWebRequest.SetRequestHeader(header.Name, header.Value);
            }
        }

        private void SignRequest(WebRequestSignInfo signInfo, UnityWebRequest unityWebRequest)
        {
            AuthChain authChain = web3Authenticator.Identity.Sign(signInfo.SignUrl);

            var i = 0;

            foreach (AuthLink link in authChain)
            {
                unityWebRequest.SetRequestHeader($"x-identity-auth-chain-{i}", link.ToJson());
                i++;
            }
        }
    }
}
