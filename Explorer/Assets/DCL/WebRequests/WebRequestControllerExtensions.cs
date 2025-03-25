﻿using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests.GenericDelete;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.DebugUtilities.UIBindings;
using DCL.WebRequests.CustomDownloadHandlers;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System.Buffers;
using UnityEngine;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public static partial class WebRequestControllerExtensions
    {
        private static readonly byte[] PARTIAL_DOWNLOAD_BUFFER = new byte[1024 * 1024];

        public static UniTask<IWebRequest> SendAsync(this ITypedWebRequest request, CancellationToken ct) =>
            request.Controller.SendAsync(request, ct);

        public static UniTask<TResult> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TWebRequestArgs args,
            TWebRequestOp op,
            CancellationToken ct,
            ReportData reportData,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null,
            ISet<long>? ignoreErrorCodes = null,
            bool suppressErrors = false,
            DownloadHandler? downloadHandler = null
        )
            where TWebRequestArgs: struct
            where TWebRequest: struct, ITypedWebRequest
            where TWebRequestOp: struct, IWebRequestOp<TWebRequest, TResult> =>
            controller.SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(
                new RequestEnvelope<TWebRequest, TWebRequestArgs>(
                    controller.requestHub.RequestDelegateFor<TWebRequestArgs, TWebRequest>(),
                    commonArguments,
                    args,
                    ct,
                    reportData,
                    headersInfo ?? WebRequestHeadersInfo.NewEmpty(),
                    signInfo,
                    ignoreErrorCodes,
                    suppressErrors,
                    downloadHandler
                ), op
            )!;

        /// <summary>
        ///     Make a generic get request to download arbitrary data
        /// </summary>
        public static GenericGetRequest GetAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            ReportData reportData,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null,
            ISet<long>? ignoreErrorCodes = null
        ) =>
            new (new RequestEnvelope<GenericGetArguments>(commonArguments, new GenericGetArguments(), reportData, headersInfo, signInfo, ignoreErrorCodes), controller);

        public static UniTask<PartialDownloadedData> GetPartialAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            CancellationToken ct,
            ReportData reportData,
            ArrayPool<byte> buffersPool,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null,
            ISet<long>? ignoreErrorCodes = null
        )
        {
            PartialDownloadHandler handler = new PartialDownloadHandler(PARTIAL_DOWNLOAD_BUFFER, buffersPool);
            return controller.SendAsync<PartialDownloadRequest, GenericGetArguments, PartialDownloadOp, PartialDownloadedData>(commonArguments, default(GenericGetArguments), new PartialDownloadOp(), ct, reportData, headersInfo, signInfo, ignoreErrorCodes, downloadHandler: handler, suppressErrors: true);
        }

        public static UniTask<TResult> PostAsync<TOp, TResult>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            GenericPostArguments arguments,
            CancellationToken ct,
            ReportData reportCategory,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) where TOp: struct, IWebRequestOp<GenericPostRequest, TResult> =>
            controller.SendAsync<GenericPostRequest, GenericPostArguments, TOp, TResult>(commonArguments, arguments, webRequestOp, ct, reportCategory, headersInfo, signInfo);

        public static UniTask<TResult> PutAsync<TOp, TResult>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            GenericPutArguments arguments,
            CancellationToken ct,
            ReportData reportCategory,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) where TOp: struct, IWebRequestOp<GenericPutRequest, TResult> =>
            controller.SendAsync<GenericPutRequest, GenericPutArguments, TOp, TResult>(commonArguments, arguments, webRequestOp, ct, reportCategory, headersInfo, signInfo);

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
            controller.SendAsync<GenericDeleteRequest, GenericDeleteArguments, TOp, TResult>(commonArguments, arguments, webRequestOp, ct, reportCategory, headersInfo, signInfo);

        public static UniTask<TResult> PatchAsync<TOp, TResult>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            GenericPatchArguments arguments,
            CancellationToken ct,
            ReportData reportCategory,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) where TOp: struct, IWebRequestOp<GenericPatchRequest, TResult> =>
            controller.SendAsync<GenericPatchRequest, GenericPatchArguments, TOp, TResult>(commonArguments, arguments, webRequestOp, ct, reportCategory, headersInfo, signInfo);

        public static UniTask<TResult> HeadAsync<TOp, TResult>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            GenericHeadArguments arguments,
            CancellationToken ct,
            ReportData reportCategory,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) where TOp: struct, IWebRequestOp<GenericHeadRequest, TResult> =>
            controller.SendAsync<GenericHeadRequest, GenericHeadArguments, TOp, TResult>(commonArguments, arguments, webRequestOp, ct, reportCategory, headersInfo, signInfo);

        public static async UniTask<bool> IsHeadReachableAsync(this IWebRequestController controller, ReportData reportData, URLAddress url, CancellationToken ct)
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
                    case WebRequestUtils.FORBIDDEN_ACCESS:
                    case WebRequestUtils.NOT_FOUND:
                        return false;
                }

                // Assume everything else means that there is such an endpoint for Non-HEAD ops
                return true;
            }
            catch (Exception) { return false; }

            return true;
        }

        public static async UniTask<bool> IsGetReachableAsync(this IWebRequestController controller, ReportData reportData, URLAddress url, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();

            try { await GetAsync<WebRequestUtils.NoOp<GenericGetRequest>, WebRequestUtils.NoResult>(controller, new CommonArguments(url), new WebRequestUtils.NoOp<GenericGetRequest>(), ct, reportData); }
            catch (UnityWebRequestException) { return false; }

            return true;
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
            WebRequestSignInfo? signInfo = null
        ) =>
            controller.requestHub.RequestDelegateFor<GetTextureArguments, GetTextureWebRequest>()(controller, new RequestEnvelope<GetTextureArguments>(
                commonArguments,
                args,
                reportData,
                headersInfo,
                signInfo
            ));

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
            controller.SendAsync<GetAudioClipWebRequest, GetAudioClipArguments, TOp, AudioClip>(commonArguments, args, webRequestOp, ct, reportData, headersInfo, signInfo);

        public static UniTask<AssetBundleLoadingResult> GetAssetBundleAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GetAssetBundleArguments args,
            CancellationToken ct,
            string reportCategory = ReportCategory.ASSET_BUNDLES,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null,
            bool suppressErrors = false) =>
            controller.SendAsync<GetAssetBundleWebRequest, GetAssetBundleArguments, GetAssetBundleWebRequest.CreateAssetBundleOp, AssetBundleLoadingResult>(commonArguments, args, new GetAssetBundleWebRequest.CreateAssetBundleOp(), ct, reportCategory, headersInfo, signInfo, suppressErrors: suppressErrors);

        public static IWebRequestController WithArtificialDelay(this IWebRequestController origin, ArtificialDelayWebRequestController.IReadOnlyOptions options) =>
            new ArtificialDelayWebRequestController(origin, options);

        public static IWebRequestController WithLog(this IWebRequestController origin) =>
            new LogWebRequestController(origin);

        public static IWebRequestController WithDebugMetrics(this IWebRequestController origin,
            ElementBinding<ulong> requestCannotConnectDebugMetric, ElementBinding<ulong> requestCompleteDebugMetric) =>
            new DebugMetricsWebRequestController(origin, requestCannotConnectDebugMetric,
                requestCompleteDebugMetric);

        public static IWebRequestController WithBudget(this IWebRequestController origin, int totalBudget, ElementBinding<ulong> debugBudget)
        {
            return new BudgetedWebRequestController(origin, totalBudget, debugBudget);
        }
    }
}
