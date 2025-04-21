using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System.Threading;
using DCL.DebugUtilities.UIBindings;
using DCL.Utilities.Extensions;
using System;
using Utility.Types;

namespace DCL.WebRequests
{
    public static class WebRequestControllerExtensions
    {
        /// <summary>
        ///     Sends the web request and awaits its execution, it's up to the consumer to dispose the resulting request
        /// </summary>
        public static UniTask<IWebRequest> SendAsync(this ITypedWebRequest request, CancellationToken ct) =>
            request.Controller.SendAsync(request, true, ct);

        /// <summary>
        ///     Send the web request, awaits its execution, and disposes of its internals. Does not provide any output that can be used
        /// </summary>
        /// <param name="request"></param>
        /// <param name="ct"></param>
        public static async UniTask SendAndForgetAsync(this ITypedWebRequest request, CancellationToken ct)
        {
            using (await request.Controller.SendAsync(request, false, ct)) ;
        }

        public static TRequest Create<TRequest, TArgs>(this IWebRequestController controller,
            TArgs args,
            CommonArguments commonArguments,
            ReportData reportData,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null,
            bool suppressErrors = false,
            Action<IWebRequest>? onRequestCreated = null) where TRequest: ITypedWebRequest<TArgs> where TArgs: struct =>
            controller.requestHub.RequestDelegateFor<TArgs, TRequest>()(controller,
                new RequestEnvelope(commonArguments, reportData, headersInfo, signInfo, suppressErrors, onRequestCreated), args);

        /// <summary>
        ///     Make a generic get request to download arbitrary data
        /// </summary>
        public static GenericGetRequest GetAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            ReportData reportData,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null,
            bool suppressErrors = false,
            Action<IWebRequest>? onRequestCreated = null
        ) =>
            controller.Create<GenericGetRequest, GenericGetArguments>(new GenericGetArguments(), commonArguments, reportData, headersInfo, signInfo, suppressErrors, onRequestCreated);

        public static GenericPostRequest PostAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GenericUploadArguments arguments,
            ReportData reportCategory,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            controller.Create<GenericPostRequest, GenericUploadArguments>(arguments, commonArguments, reportCategory, headersInfo, signInfo);

        public static GenericPutRequest PutAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GenericUploadArguments arguments,
            ReportData reportCategory,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            controller.Create<GenericPutRequest, GenericUploadArguments>(arguments, commonArguments, reportCategory, headersInfo, signInfo);

        public static GenericDeleteRequest DeleteAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GenericUploadArguments arguments,
            ReportData reportCategory,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            controller.Create<GenericDeleteRequest, GenericUploadArguments>(arguments, commonArguments, reportCategory, headersInfo, signInfo);

        public static GenericPatchRequest PatchAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GenericUploadArguments arguments,
            ReportData reportCategory,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            controller.Create<GenericPatchRequest, GenericUploadArguments>(arguments, commonArguments, reportCategory, headersInfo, signInfo);

        public static GenericHeadRequest HeadAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            ReportData reportCategory,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            controller.Create<GenericHeadRequest, GenericHeadArguments>(new GenericHeadArguments(), commonArguments, reportCategory, headersInfo, signInfo);

        public static async UniTask<Result> IsHeadReachableAsync(this IWebRequestController controller, ReportData reportData, URLAddress url, CancellationToken ct)
        {
            try
            {
                await controller.HeadAsync(url, reportData).SendAndForgetAsync(ct);
                return Result.SuccessResult();
            }
            catch (WebRequestException e)
            {
                // Endpoint was unreacheable
                if (!e.ResponseReceived)
                    return Result.ErrorResult($"{url} is unreachable");

                // HEAD request might not be fully supported by the streaming platforms
                switch (e.ResponseCode)
                {
                    // It means there is no such end-point at all
                    case WebRequestUtils.BAD_REQUEST:
                    case WebRequestUtils.FORBIDDEN_ACCESS:
                    case WebRequestUtils.NOT_FOUND:
                    case WebRequestUtils.INTERNAL_SERVER_ERROR:
                        return Result.ErrorResult($"{url} responded with {e.ResponseCode}");
                }

                // Assume everything else means that there is such an endpoint for Non-HEAD ops
                return Result.SuccessResult();
            }
        }

        /// <summary>
        ///     Get is Reachable when the downloading started
        /// </summary>
        public static async UniTask<bool> IsGetReachableAsync(this IWebRequestController controller, ReportData reportData, URLAddress url, CancellationToken ct)
        {
            var downloadStarted = false;

            void OnCreated(IWebRequest webRequest)
            {
                webRequest.OnDownloadStarted += _ => downloadStarted = true;
            }

            // We are not interested in exception
            GenericGetRequest getRequest = controller.GetAsync(url, reportData, suppressErrors: true, onRequestCreated: OnCreated);
            await getRequest.SendAndForgetAsync(ct).SuppressToResultAsync();

            return downloadStarted;
        }

        /// <summary>
        ///     Make a request that is optimized for texture creation
        /// </summary>
        public static GetTextureWebRequest GetTextureAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GetTextureArguments args,
            ReportData reportData,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null,
            bool suppressErrors = false
        ) =>
            controller.requestHub.RequestDelegateFor<GetTextureArguments, GetTextureWebRequest>()(controller, new RequestEnvelope(
                commonArguments,
                reportData,
                headersInfo,
                signInfo,
                suppressErrors
            ), args);

        /// <summary>
        ///     Make a request that is optimized for audio clip
        /// </summary>
        public static GetAudioClipWebRequest GetAudioClipAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GetAudioClipArguments args,
            ReportData reportData,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            controller.Create<GetAudioClipWebRequest, GetAudioClipArguments>(args, commonArguments, reportData, headersInfo, signInfo);

        public static GetAssetBundleWebRequest GetAssetBundleAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GetAssetBundleArguments args,
            string reportCategory = ReportCategory.ASSET_BUNDLES,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null,
            bool suppressErrors = false) =>
            controller.requestHub.RequestDelegateFor<GetAssetBundleArguments, GetAssetBundleWebRequest>()(controller, new RequestEnvelope(
                commonArguments,
                new ReportData(reportCategory),
                headersInfo,
                signInfo,
                suppressErrors: suppressErrors
            ), args);

        public static IWebRequestController WithArtificialDelay(this IWebRequestController origin, ArtificialDelayWebRequestController.IReadOnlyOptions options) =>
            new ArtificialDelayWebRequestController(origin, options);

        public static IWebRequestController WithLog(this IWebRequestController origin) =>
            new LogWebRequestController(origin);

        public static IWebRequestController WithBudget(this IWebRequestController origin, int totalBudget, ElementBinding<ulong> debugBudget) =>
            new DisposeRequestWrap(new BudgetedWebRequestController(origin, totalBudget, debugBudget));
    }
}
