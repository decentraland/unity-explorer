using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.WebRequests.GenericDelete;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.DebugUtilities.UIBindings;
using UnityEngine;
using UnityEngine.Networking;
using Utility.Times;

namespace DCL.WebRequests
{
    public static class WebRequestControllerExtensions
    {
        internal static readonly InitializeRequest<GenericGetArguments, GenericGetRequest> GET_GENERIC = GenericGetRequest.Initialize;
        internal static readonly InitializeRequest<GenericPostArguments, GenericPostRequest> POST_GENERIC = GenericPostRequest.Initialize;
        internal static readonly InitializeRequest<GenericPutArguments, GenericPutRequest> PUT_GENERIC = GenericPutRequest.Initialize;
        internal static readonly InitializeRequest<GenericDeleteArguments, GenericDeleteRequest> DELETE_GENERIC = GenericDeleteRequest.Initialize;
        internal static readonly InitializeRequest<GenericPatchArguments, GenericPatchRequest> PATCH_GENERIC = GenericPatchRequest.Initialize;
        internal static readonly InitializeRequest<GenericHeadArguments, GenericHeadRequest> HEAD_GENERIC = GenericHeadRequest.Initialize;

        internal static readonly InitializeRequest<GetTextureArguments, GetTextureWebRequest> GET_TEXTURE = GetTextureWebRequest.Initialize;
        private static readonly InitializeRequest<GetAudioClipArguments, GetAudioClipWebRequest> GET_AUDIO_CLIP = GetAudioClipWebRequest.Initialize;
        private static readonly InitializeRequest<GetAssetBundleArguments, GetAssetBundleWebRequest> GET_ASSET_BUNDLE = GetAssetBundleWebRequest.Initialize;

        public static UniTask<TResult> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(
            this IWebRequestController controller,
            InitializeRequest<TWebRequestArgs, TWebRequest> initializeRequest,
            CommonArguments commonArguments, TWebRequestArgs args,
            TWebRequestOp op,
            CancellationToken ct,
            ReportData reportData,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null,
            ISet<long>? ignoreErrorCodes = null,
            bool suppressErrors = false
        )
            where TWebRequestArgs: struct
            where TWebRequest: struct, ITypedWebRequest
            where TWebRequestOp: struct, IWebRequestOp<TWebRequest, TResult> =>
            controller.SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(
                new RequestEnvelope<TWebRequest, TWebRequestArgs>(
                    initializeRequest,
                    commonArguments,
                    args,
                    ct,
                    reportData,
                    headersInfo ?? WebRequestHeadersInfo.NewEmpty(),
                    signInfo,
                    ignoreErrorCodes,
                    suppressErrors
                ), op
            );

        public static UniTask<TResult> SignedFetchPostAsync<TOp, TResult>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            string signatureMetadata,
            ReportData reportData,
            CancellationToken ct
        )
            where TOp: struct, IWebRequestOp<GenericPostRequest, TResult>
        {
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            return controller.PostAsync<TOp, TResult>(
                commonArguments,
                webRequestOp,
                GenericPostArguments.Empty,
                ct,
                reportData,
                signInfo: WebRequestSignInfo.NewFromRaw(signatureMetadata, commonArguments.URL, unixTimestamp, "post"),
                headersInfo: new WebRequestHeadersInfo().WithSign(signatureMetadata, unixTimestamp)
            );
        }

        /// <summary>
        ///     Make a generic get request to download arbitrary data
        /// </summary>
        public static UniTask<TResult> GetAsync<TOp, TResult>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            CancellationToken ct,
            ReportData reportData,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null,
            ISet<long>? ignoreErrorCodes = null
        ) where TOp: struct, IWebRequestOp<GenericGetRequest, TResult> =>
            controller.SendAsync<GenericGetRequest, GenericGetArguments, TOp, TResult>(GET_GENERIC, commonArguments, default(GenericGetArguments), webRequestOp, ct, reportData, headersInfo, signInfo, ignoreErrorCodes);

        public static UniTask<TResult> PostAsync<TOp, TResult>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            GenericPostArguments arguments,
            CancellationToken ct,
            ReportData reportCategory,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) where TOp: struct, IWebRequestOp<GenericPostRequest, TResult> =>
            controller.SendAsync<GenericPostRequest, GenericPostArguments, TOp, TResult>(POST_GENERIC, commonArguments, arguments, webRequestOp, ct, reportCategory, headersInfo, signInfo);

        public static UniTask<TResult> PutAsync<TOp, TResult>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            GenericPutArguments arguments,
            CancellationToken ct,
            ReportData reportCategory,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) where TOp: struct, IWebRequestOp<GenericPutRequest, TResult> =>
            controller.SendAsync<GenericPutRequest, GenericPutArguments, TOp, TResult>(PUT_GENERIC, commonArguments, arguments, webRequestOp, ct, reportCategory, headersInfo, signInfo);

        public static UniTask<TResult> DeleteAsync<TOp, TResult>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            GenericDeleteArguments arguments,
            CancellationToken ct,
            ReportData reportCategory,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null
        ) where TOp: struct, IWebRequestOp<GenericDeleteRequest, TResult> =>
            controller.SendAsync<GenericDeleteRequest, GenericDeleteArguments, TOp, TResult>(DELETE_GENERIC, commonArguments, arguments, webRequestOp, ct, reportCategory, headersInfo, signInfo);

        public static UniTask<TResult> PatchAsync<TOp, TResult>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            GenericPatchArguments arguments,
            CancellationToken ct,
            ReportData reportCategory,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) where TOp: struct, IWebRequestOp<GenericPatchRequest, TResult> =>
            controller.SendAsync<GenericPatchRequest, GenericPatchArguments, TOp, TResult>(PATCH_GENERIC, commonArguments, arguments, webRequestOp, ct, reportCategory, headersInfo, signInfo);

        public static UniTask<TResult> HeadAsync<TOp, TResult>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            GenericHeadArguments arguments,
            CancellationToken ct,
            ReportData reportCategory,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) where TOp: struct, IWebRequestOp<GenericHeadRequest, TResult> =>
            controller.SendAsync<GenericHeadRequest, GenericHeadArguments, TOp, TResult>(HEAD_GENERIC, commonArguments, arguments, webRequestOp, ct, reportCategory, headersInfo, signInfo);

        public static async UniTask<bool> IsReachableAsync(this IWebRequestController controller, ReportData reportData, URLAddress url, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();

            try { await HeadAsync<WebRequestUtils.NoOp<GenericHeadRequest>, WebRequestUtils.NoResult>(controller, new CommonArguments(url), new WebRequestUtils.NoOp<GenericHeadRequest>(), default(GenericHeadArguments), ct, reportData); }
            catch (UnityWebRequestException unityWebRequestException)
            {
                // Endpoint was unreacheable
                if (unityWebRequestException.Result == UnityWebRequest.Result.ConnectionError)
                    return false;

                // HEAD request might not be fully supported by the streaming platforms
                switch (unityWebRequestException.ResponseCode)
                {
                    // It means there is no such end-point at all
                    case WebRequestUtils.BAD_REQUEST:
                    case WebRequestUtils.NOT_FOUND:
                        return false;
                }

                // Assume everything else means that there is such an endpoint for Non-HEAD ops
                return true;
            }
            catch (Exception) { return false; }

            return true;
        }

        /// <summary>
        ///     Make a request that is optimized for texture creation
        /// </summary>
        public static UniTask<Texture2D> GetTextureAsync<TOp>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GetTextureArguments args,
            TOp webRequestOp,
            CancellationToken ct,
            ReportData reportData,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null
        ) where TOp: struct, IWebRequestOp<GetTextureWebRequest, Texture2D> =>
            controller.SendAsync<GetTextureWebRequest, GetTextureArguments, TOp, Texture2D>(GET_TEXTURE, commonArguments, args, webRequestOp, ct, reportData, headersInfo, signInfo);

        /// <summary>
        ///     Make a request that is optimized for audio clip
        /// </summary>
        public static UniTask<AudioClip> GetAudioClipAsync<TOp>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GetAudioClipArguments args,
            TOp webRequestOp,
            CancellationToken ct,
            ReportData reportData,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) where TOp: struct, IWebRequestOp<GetAudioClipWebRequest, AudioClip> =>
            controller.SendAsync<GetAudioClipWebRequest, GetAudioClipArguments, TOp, AudioClip>(GET_AUDIO_CLIP, commonArguments, args, webRequestOp, ct, reportData, headersInfo, signInfo);

        public static UniTask<AssetBundleLoadingResult> GetAssetBundleAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GetAssetBundleArguments args,
            CancellationToken ct,
            string reportCategory = ReportCategory.ASSET_BUNDLES,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null,
            bool suppressErrors = false) =>
            controller.SendAsync<GetAssetBundleWebRequest, GetAssetBundleArguments, GetAssetBundleWebRequest.CreateAssetBundleOp, AssetBundleLoadingResult>(GET_ASSET_BUNDLE, commonArguments, args, new GetAssetBundleWebRequest.CreateAssetBundleOp(), ct, reportCategory, headersInfo, signInfo, suppressErrors: suppressErrors);

        public static IWebRequestController WithArtificialDelay(this IWebRequestController origin, ArtificialDelayWebRequestController.IReadOnlyOptions options) =>
            new ArtificialDelayWebRequestController(origin, options);

        public static IWebRequestController WithLog(this IWebRequestController origin) =>
            new LogWebRequestController(origin);

        public static IWebRequestController WithDebugMetrics(this IWebRequestController origin,
            ElementBinding<ulong> requestCannotConnectDebugMetric, ElementBinding<ulong> requestCompleteDebugMetric)
        {
            return new DebugMetricsWebRequestController(origin, requestCannotConnectDebugMetric,
                requestCompleteDebugMetric);
        }

        public static IWebRequestController WithBudget(this IWebRequestController origin, int totalBudget)
        {
            return new BudgetedWebRequestController(origin, totalBudget);
        }

    }
}
