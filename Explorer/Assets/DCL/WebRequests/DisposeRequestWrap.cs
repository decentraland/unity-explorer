using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests.RequestsHub;
using System;
using System.Threading;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Ensure the request wrap is disposed of on exceptions
    /// </summary>
    public class DisposeRequestWrap : IWebRequestController
    {
        private readonly IWebRequestController origin;

        public DisposeRequestWrap(IWebRequestController origin)
        {
            this.origin = origin;
        }

        IRequestHub IWebRequestController.requestHub => origin.requestHub;

        public async UniTask<IWebRequest> SendAsync(ITypedWebRequest requestWrap, bool detachDownloadHandler, CancellationToken ct)
        {
            try { return await origin.SendAsync(requestWrap, detachDownloadHandler, ct); }
            catch (Exception)
            {
                requestWrap.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            origin.Dispose();
        }
    }
}
