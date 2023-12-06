using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
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

        /// <summary>
        ///     Make a request that is optimized for texture creation
        /// </summary>
        UniTask<GetTextureWebRequest> GetTextureAsync(
            CommonArguments commonArguments,
            GetTextureArguments args,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null);
    }
}
