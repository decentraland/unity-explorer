using Cysharp.Threading.Tasks;
using DCL.WebRequests.HTTP2;
using DCL.WebRequests.RequestsHub;
using System;
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
        private readonly IWebRequestController yetAnotherWebRequestController;

        IRequestHub IWebRequestController.requestHub => requestHub;

        public RedirectWebRequestController(WebRequestsMode mode,
            IWebRequestController unityWebRequestController,
            IWebRequestController http2WebRequestController,
            IWebRequestController yetAnotherWebRequestController,
            IRequestHub requestHub)
        {
            this.mode = mode;
            this.unityWebRequestController = unityWebRequestController;
            this.http2WebRequestController = http2WebRequestController;
            this.yetAnotherWebRequestController = yetAnotherWebRequestController;
            this.requestHub = requestHub;
        }

        public UniTask<IWebRequest> SendAsync(ITypedWebRequest requestWrap, bool detachDownloadHandler, CancellationToken ct)
        {
            if (requestWrap.Http2Supported)
            {
                switch (mode)
                {
                    // Yet Another doesn't support files => use UnityWebRequest
                    case WebRequestsMode.YET_ANOTHER when !requestWrap.Envelope.CommonArguments.URL.IsFile:
                        return yetAnotherWebRequestController.SendAsync(requestWrap, detachDownloadHandler, ct);
                    case WebRequestsMode.HTTP2:
                        return http2WebRequestController.SendAsync(requestWrap, detachDownloadHandler, ct);
                }
            }

            return unityWebRequestController.SendAsync(requestWrap, detachDownloadHandler, ct);
        }

        public void Dispose()
        {
            unityWebRequestController.Dispose();
            http2WebRequestController.Dispose();
            yetAnotherWebRequestController.Dispose();
        }
    }
}
