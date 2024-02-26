using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
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
    }
}
