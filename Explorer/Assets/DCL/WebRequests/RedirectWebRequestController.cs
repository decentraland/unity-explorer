using Cysharp.Threading.Tasks;
using DCL.WebRequests.HTTP2;
using DCL.WebRequests.RequestsHub;
using System.Threading;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Based on the request type redirects to <see cref="DefaultWebRequestController" /> or <see cref="Http2WebRequestController" />
    /// </summary>
    public class RedirectWebRequestController : IWebRequestController
    {
        private readonly WebRequestsMode mode;
        private readonly IRequestHub requestHub;

        private readonly IWebRequestController unityWebRequestController;
        private readonly IWebRequestController http2WebRequestController;

        IRequestHub IWebRequestController.requestHub => requestHub;

        public RedirectWebRequestController(WebRequestsMode mode,
            IWebRequestController unityWebRequestController,
            IWebRequestController http2WebRequestController,
            IRequestHub requestHub)
        {
            this.mode = mode;
            this.unityWebRequestController = unityWebRequestController;
            this.http2WebRequestController = http2WebRequestController;
            this.requestHub = requestHub;
        }

        public UniTask<IWebRequest> SendAsync(ITypedWebRequest requestWrap, bool detachDownloadHandler, CancellationToken ct)
        {
            if (mode == WebRequestsMode.HTTP2 && requestWrap.Http2Supported)
                return http2WebRequestController.SendAsync(requestWrap, detachDownloadHandler, ct);

            return unityWebRequestController.SendAsync(requestWrap, detachDownloadHandler, ct);
        }
    }
}
