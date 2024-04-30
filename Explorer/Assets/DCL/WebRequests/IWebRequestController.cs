using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using DCL.WebRequests.GenericHead;
using System;
using System.Collections.Generic;
using System.Threading;
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

        UniTask<TWebRequestOp> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp>(RequestEnvelope<TWebRequest, TWebRequestArgs> envelope, TWebRequestOp op)
            where TWebRequestArgs: struct
            where TWebRequest: struct, ITypedWebRequest
            where TWebRequestOp: IWebRequestOp<TWebRequest>;
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

        public static UniTask<TWebRequestOp> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp>(
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
            where TWebRequestOp: struct, IWebRequestOp<TWebRequest> =>
            controller.SendAsync(
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

        public static UniTask<TOp> SignedFetchPostAsync<TOp>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            string jsonMetaData,
            CancellationToken ct
        )
            where TOp: struct, IWebRequestOp<GenericPostRequest>
        {
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            return controller.PostAsync(
                commonArguments,
                webRequestOp,
                GenericPostArguments.Empty,
                ct,
                signInfo: WebRequestSignInfo.NewFromRaw(jsonMetaData, commonArguments.URL, unixTimestamp, "post"),
                headersInfo: new WebRequestHeadersInfo().WithSign(jsonMetaData, unixTimestamp)
            );
        }

        public static UniTask<TOp> SignedFetchPostAsync<TOp>(
            this IWebRequestController controller,
            string url,
            TOp webRequestOp,
            string jsonMetaData,
            CancellationToken ct
        )
            where TOp: struct, IWebRequestOp<GenericPostRequest> =>
            controller.SignedFetchPostAsync(new CommonArguments(URLAddress.FromString(url)), webRequestOp, jsonMetaData, ct);

        /// <summary>
        ///     Make a generic get request to download arbitrary data
        /// </summary>
        public static UniTask<TOp> GetAsync<TOp>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null,
            ISet<long>? ignoreErrorCodes = null
        ) where TOp: struct, IWebRequestOp<GenericGetRequest> =>
            controller.SendAsync(GET_GENERIC, commonArguments, default(GenericGetArguments), webRequestOp, ct, reportCategory, headersInfo, signInfo, ignoreErrorCodes);

        public static UniTask<TOp> PostAsync<TOp>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            GenericPostArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) where TOp: struct, IWebRequestOp<GenericPostRequest> =>
            controller.SendAsync(POST_GENERIC, commonArguments, arguments, webRequestOp, ct, reportCategory, headersInfo, signInfo);

        public static UniTask<TOp> PutAsync<TOp>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            GenericPutArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) where TOp: struct, IWebRequestOp<GenericPutRequest> =>
            controller.SendAsync(PUT_GENERIC, commonArguments, arguments, webRequestOp, ct, reportCategory, headersInfo, signInfo);

        public static UniTask<TOp> PatchAsync<TOp>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            GenericPatchArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) where TOp: struct, IWebRequestOp<GenericPatchRequest> =>
            controller.SendAsync(PATCH_GENERIC, commonArguments, arguments, webRequestOp, ct, reportCategory, headersInfo, signInfo);

        public static UniTask<TOp> HeadAsync<TOp>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            GenericHeadArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) where TOp: struct, IWebRequestOp<GenericHeadRequest> =>
            controller.SendAsync(HEAD_GENERIC, commonArguments, arguments, webRequestOp, ct, reportCategory, headersInfo, signInfo);

        public static UniTask<TOp> GetTextureAsync<TOp>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GetTextureArguments args,
            TOp webRequestOp,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null
        ) where TOp: struct, IWebRequestOp<GetTextureWebRequest> =>
            controller.SendAsync(GET_TEXTURE, commonArguments, args, webRequestOp, ct, reportCategory, headersInfo, signInfo);

        /// <summary>
        ///     Make a request that is optimized for audio clip
        /// </summary>
        public static UniTask<TOp> GetAudioClipAsync<TOp>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GetAudioClipArguments args,
            TOp webRequestOp,
            CancellationToken ct,
            string reportCategory = ReportCategory.AUDIO_CLIP_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) where TOp: struct, IWebRequestOp<GetAudioClipWebRequest> =>
            controller.SendAsync(GET_AUDIO_CLIP, commonArguments, args, webRequestOp, ct, reportCategory, headersInfo, signInfo);
    }
}
