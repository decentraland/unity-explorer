using Cysharp.Threading.Tasks;
using Diagnostics.ReportsHandling;
using System.Threading;

namespace DCL.WebRequests
{
    public interface IWebRequestController
    {
        UniTask<GetTextureWebRequest> GetTextureAsync(
            CommonArguments commonArguments,
            GetTextureArguments args,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null);
    }
}
