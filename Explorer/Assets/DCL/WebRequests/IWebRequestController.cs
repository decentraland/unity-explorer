using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using DCL.WebRequests.AudioClips;
using DCL.WebRequests.GenericHead;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility.Times;

namespace DCL.WebRequests
{
    public interface IWebRequestController
    {
        UniTask<TWebRequest> SendAsync<TWebRequest, TWebRequestArgs>(RequestEnvelope<TWebRequest, TWebRequestArgs> envelope)
            where TWebRequestArgs: struct
            where TWebRequest: struct, ITypedWebRequest;

        static readonly IWebRequestController DEFAULT = new WebRequestController(
            new PlayerPrefsIdentityProvider(
                new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer()
            )
        );

        public static readonly ISet<long> IGNORE_NOT_FOUND = new HashSet<long> { 404 };
    }

    public static class WebRequestControllerExtensions
    {
        private static readonly InitializeRequest<GenericGetArguments, GenericGetRequest> GET_GENERIC = GenericGetRequest.Initialize;
        private static readonly InitializeRequest<GenericPostArguments, GenericPostRequest> POST_GENERIC = GenericPostRequest.Initialize;
        private static readonly InitializeRequest<GenericPutArguments, GenericPutRequest> PUT_GENERIC = GenericPutRequest.Initialize;
        private static readonly InitializeRequest<GenericPatchArguments, GenericPatchRequest> PATCH_GENERIC = GenericPatchRequest.Initialize;
        private static readonly InitializeRequest<GenericHeadArguments, GenericHeadRequest> HEAD_GENERIC = GenericHeadRequest.Initialize;

        private static readonly InitializeRequest<GetTextureArguments, GetTextureWebRequest> GET_TEXTURE = GetTextureWebRequest.Initialize;
        private static readonly InitializeRequest<GetAudioClipArguments, GetAudioClipWebRequest> GET_AUDIO_CLIP = GetAudioClipWebRequest.Initialize;

        public static UniTask<TWebRequest> SendAsync<TWebRequest, TWebRequestArgs>(
            this IWebRequestController controller,
            InitializeRequest<TWebRequestArgs, TWebRequest> initializeRequest,
            CommonArguments commonArguments, TWebRequestArgs args,
            CancellationToken ct,
            string reportCategory,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null,
            ISet<long>? ignoreErrorCodes = null
        )
            where TWebRequestArgs: struct
            where TWebRequest: struct, ITypedWebRequest =>
            controller.SendAsync(
                new RequestEnvelope<TWebRequest, TWebRequestArgs>(
                    initializeRequest,
                    commonArguments,
                    args,
                    ct,
                    reportCategory,
                    headersInfo,
                    signInfo,
                    ignoreErrorCodes
                )
            );

        public static UniTask<GenericPostRequest> SignedFetchAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            string jsonMetaData,
            CancellationToken ct
        )
        {
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();
            string path = new Uri(commonArguments.URL).AbsolutePath;
            string payload = $"post:{path}:{unixTimestamp}:{jsonMetaData}".ToLower();

            return controller.PostAsync(
                commonArguments,
                GenericPostArguments.Empty,
                ct,
                signInfo: new WebRequestSignInfo(payload),
                headersInfo: new WebRequestHeadersInfo()
                            .Add("x-identity-timestamp", unixTimestamp.ToString()!)
                            .Add("x-identity-metadata", jsonMetaData)
            );
        }

        public static UniTask<GenericPostRequest> SignedFetchAsync(
            this IWebRequestController controller,
            string url,
            string jsonMetaData,
            CancellationToken ct
        ) =>
            controller.SignedFetchAsync(new CommonArguments(URLAddress.FromString(url)), jsonMetaData, ct);

        /// <summary>
        ///     Make a generic get request to download arbitrary data
        /// </summary>
        public static UniTask<GenericGetRequest> GetAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null,
            ISet<long>? ignoreErrorCodes = null
        ) =>
            controller.SendAsync(GET_GENERIC, commonArguments, default(GenericGetArguments), ct, reportCategory, headersInfo, signInfo, ignoreErrorCodes);

        public static UniTask<GenericPostRequest> PostAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GenericPostArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            controller.SendAsync(POST_GENERIC, commonArguments, arguments, ct, reportCategory, headersInfo, signInfo);

        public static UniTask<GenericPutRequest> PutAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GenericPutArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            controller.SendAsync(PUT_GENERIC, commonArguments, arguments, ct, reportCategory, headersInfo, signInfo);

        public static UniTask<GenericPatchRequest> PatchAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GenericPatchArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            controller.SendAsync(PATCH_GENERIC, commonArguments, arguments, ct, reportCategory, headersInfo, signInfo);

        public static UniTask<GenericHeadRequest> HeadAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GenericHeadArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            controller.SendAsync(HEAD_GENERIC, commonArguments, arguments, ct, reportCategory, headersInfo, signInfo);

        /// <summary>
        ///     Make a request that is optimized for texture creation
        /// </summary>
        public static UniTask<GetTextureWebRequest> GetTextureAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GetTextureArguments args,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            controller.SendAsync(GET_TEXTURE, commonArguments, args, ct, reportCategory, headersInfo, signInfo);

        /// <summary>
        ///     Make a request that is optimized for audio clip
        /// </summary>
        public static UniTask<GetAudioClipWebRequest> GetAudioClipAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GetAudioClipArguments args,
            CancellationToken ct,
            string reportCategory = ReportCategory.AUDIO_CLIP_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            controller.SendAsync(GET_AUDIO_CLIP, commonArguments, args, ct, reportCategory, headersInfo, signInfo);
    }
}
