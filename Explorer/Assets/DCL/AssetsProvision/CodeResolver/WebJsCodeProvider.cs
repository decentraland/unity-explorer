using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System;
using System.Threading;
using UnityEngine.Networking;

namespace DCL.AssetsProvision.CodeResolver
{
    public class WebJsCodeProvider
    {
        private readonly IWebRequestController webRequestController;

        public WebJsCodeProvider(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public async UniTask<DownloadHandler> GetJsCodeAsync(URLAddress url,
            CancellationToken cancellationToken = default)
        {
            var request = webRequestController.GetAsync(new CommonArguments(url), cancellationToken,
                    ReportCategory.SCENE_LOADING);

            string? contentType = await request.GetResponseHeaderAsync("Content-Type");

            if (contentType != null
                && contentType.Contains("charset", StringComparison.InvariantCultureIgnoreCase)
                && !contentType.Contains("utf-8", StringComparison.InvariantCultureIgnoreCase))
                throw new NotImplementedException($"Can't handle content type {contentType}");

            return await request.ExposeDownloadHandlerAsync();
        }
    }
}
