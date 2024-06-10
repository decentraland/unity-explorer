using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility.Times;

namespace DCL.WebRequests
{
    public interface IWebRequestController
    {
        static readonly IWebRequestController DEFAULT = new WebRequestController(
            new PlayerPrefsIdentityProvider(
                new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer()
            )
        );

        public static readonly ISet<long> IGNORE_NOT_FOUND = new HashSet<long> { 404 };

        UniTask<TResult?> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(RequestEnvelope<TWebRequest, TWebRequestArgs> envelope, TWebRequestOp op)
            where TWebRequestArgs: struct
            where TWebRequest: struct, ITypedWebRequest
            where TWebRequestOp: IWebRequestOp<TWebRequest, TResult>;
    }

    public static class WebRequestControllerExtensions
    {
        internal static readonly InitializeRequest<GenericGetArguments, GenericGetRequest> GET_GENERIC = GenericGetRequest.Initialize;
        internal static readonly InitializeRequest<GenericPostArguments, GenericPostRequest> POST_GENERIC = GenericPostRequest.Initialize;
        internal static readonly InitializeRequest<GenericPutArguments, GenericPutRequest> PUT_GENERIC = GenericPutRequest.Initialize;
        internal static readonly InitializeRequest<GenericPatchArguments, GenericPatchRequest> PATCH_GENERIC = GenericPatchRequest.Initialize;
        internal static readonly InitializeRequest<GenericHeadArguments, GenericHeadRequest> HEAD_GENERIC = GenericHeadRequest.Initialize;

        internal static readonly InitializeRequest<GetTextureArguments, GetTextureWebRequest> GET_TEXTURE = GetTextureWebRequest.Initialize;
        private static readonly InitializeRequest<GetAudioClipArguments, GetAudioClipWebRequest> GET_AUDIO_CLIP = GetAudioClipWebRequest.Initialize;

        public static UniTask<TResult> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(
            this IWebRequestController controller,
            InitializeRequest<TWebRequestArgs, TWebRequest> initializeRequest,
            CommonArguments commonArguments, TWebRequestArgs args,
            TWebRequestOp op,
            CancellationToken ct,
            string reportCategory,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null,
            ISet<long>? ignoreErrorCodes = null
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
                    reportCategory,
                    headersInfo ?? WebRequestHeadersInfo.NewEmpty(),
                    signInfo,
                    ignoreErrorCodes
                ), op
            );

        public static UniTask<TResult> SignedFetchPostAsync<TOp, TResult>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            string jsonMetaData,
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
                signInfo: WebRequestSignInfo.NewFromRaw(jsonMetaData, commonArguments.URL, unixTimestamp, "post"),
                headersInfo: new WebRequestHeadersInfo().WithSign(jsonMetaData, unixTimestamp)
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
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null,
            ISet<long>? ignoreErrorCodes = null
        ) where TOp: struct, IWebRequestOp<GenericGetRequest, TResult> =>
            controller.SendAsync<GenericGetRequest, GenericGetArguments, TOp, TResult>(GET_GENERIC, commonArguments, default(GenericGetArguments), webRequestOp, ct, reportCategory, headersInfo, signInfo, ignoreErrorCodes);

        public static UniTask<TResult> PostAsync<TOp, TResult>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            GenericPostArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) where TOp: struct, IWebRequestOp<GenericPostRequest, TResult> =>
            controller.SendAsync<GenericPostRequest, GenericPostArguments, TOp, TResult>(POST_GENERIC, commonArguments, arguments, webRequestOp, ct, reportCategory, headersInfo, signInfo);

        public static UniTask<TResult> PutAsync<TOp, TResult>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            GenericPutArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) where TOp: struct, IWebRequestOp<GenericPutRequest, TResult> =>
            controller.SendAsync<GenericPutRequest, GenericPutArguments, TOp, TResult>(PUT_GENERIC, commonArguments, arguments, webRequestOp, ct, reportCategory, headersInfo, signInfo);

        public static UniTask<TResult> PatchAsync<TOp, TResult>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            GenericPatchArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) where TOp: struct, IWebRequestOp<GenericPatchRequest, TResult> =>
            controller.SendAsync<GenericPatchRequest, GenericPatchArguments, TOp, TResult>(PATCH_GENERIC, commonArguments, arguments, webRequestOp, ct, reportCategory, headersInfo, signInfo);

        public static UniTask<TResult> HeadAsync<TOp, TResult>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            GenericHeadArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) where TOp: struct, IWebRequestOp<GenericHeadRequest, TResult> =>
            controller.SendAsync<GenericHeadRequest, GenericHeadArguments, TOp, TResult>(HEAD_GENERIC, commonArguments, arguments, webRequestOp, ct, reportCategory, headersInfo, signInfo);

        public static async UniTask<bool> IsReachableAsync(this IWebRequestController controller, URLAddress url, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();

            try { await controller.HeadAsync(new CommonArguments(url), default(GenericHeadArguments), ct).WithNoOpAsync(); }
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
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null
        ) where TOp: struct, IWebRequestOp<GetTextureWebRequest, Texture2D> =>
            controller.SendAsync<GetTextureWebRequest, GetTextureArguments, TOp, Texture2D>(GET_TEXTURE, commonArguments, args, webRequestOp, ct, reportCategory, headersInfo, signInfo);

        /// <summary>
        ///     Make a request that is optimized for audio clip
        /// </summary>
        public static UniTask<AudioClip> GetAudioClipAsync<TOp>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GetAudioClipArguments args,
            TOp webRequestOp,
            CancellationToken ct,
            string reportCategory = ReportCategory.AUDIO_CLIP_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) where TOp: struct, IWebRequestOp<GetAudioClipWebRequest, AudioClip> =>
            controller.SendAsync<GetAudioClipWebRequest, GetAudioClipArguments, TOp, AudioClip>(GET_AUDIO_CLIP, commonArguments, args, webRequestOp, ct, reportCategory, headersInfo, signInfo);

        public static IWebRequestController WithSafeMainThread(this IWebRequestController origin) =>
            new SafeMainThreadWebRequestController(origin);

        public static IWebRequestController WithArtificialDelay(this IWebRequestController origin, ArtificialDelayWebRequestController.IReadOnlyOptions options) =>
            new ArtificialDelayWebRequestController(origin, options);

        public static IWebRequestController WithLog(this IWebRequestController origin) =>
            new LogWebRequestController(origin);
    }
}
