using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using DCL.WebRequests.AudioClips;
using DCL.WebRequests.GenericHead;
using System.Threading;

namespace DCL.WebRequests
{
    public interface IWebRequestController
    {
        /// <summary>
        ///     Make a generic get request to download arbitrary data
        /// </summary>
        UniTask<GenericGetRequest> GetAsync(
            CommonArguments commonArguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null);

        UniTask<GenericPostRequest> PostAsync(
            CommonArguments commonArguments,
            GenericPostArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null);

        UniTask<GenericPutRequest> PutAsync(
            CommonArguments commonArguments,
            GenericPutArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null);

        UniTask<GenericPatchRequest> PatchAsync(
            CommonArguments commonArguments,
            GenericPatchArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null);

        UniTask<GenericHeadRequest> HeadAsync(CommonArguments commonArguments,
            GenericHeadArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null);

        /// <summary>
        ///     Make a request that is optimized for texture creation
        /// </summary>
        UniTask<GetTextureWebRequest> GetTextureAsync(
            CommonArguments commonArguments,
            GetTextureArguments args,
            CancellationToken ct,
            string reportCategory = ReportCategory.TEXTURE_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null);

        /// <summary>
        ///     Make a request that is optimized for audio clip
        /// </summary>
        UniTask<GetAudioClipWebRequest> GetAudioClipAsync(
            CommonArguments commonArguments,
            GetAudioClipArguments args,
            CancellationToken ct,
            string reportCategory = ReportCategory.AUDIO_CLIP_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null);

        static readonly IWebRequestController DEFAULT = new WebRequestController(
            new PlayerPrefsIdentityProvider(
                new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer()
            )
        );
    }

    public static class WebRequestControllerExtensions
    {
        public static UniTask<GenericPostRequest> SignedFetch(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            string? jsonMetaData,
            CancellationToken ct
        ) =>
            controller.PostAsync(
                commonArguments,
                GenericPostArguments.Empty,
                ct,
                signInfo: jsonMetaData is null ? null : new WebRequestSignInfo(jsonMetaData)
            );

        public static UniTask<GenericPostRequest> SignedFetch(
            this IWebRequestController controller,
            string url,
            string? jsonMetaData,
            CancellationToken ct
        ) =>
            controller.SignedFetch(new CommonArguments(URLAddress.FromString(url)), jsonMetaData, ct);
    }
}
