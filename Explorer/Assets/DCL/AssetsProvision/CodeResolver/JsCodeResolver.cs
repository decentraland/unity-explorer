using AssetManagement;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AssetsProvision.CodeResolver
{
    public class JsCodeResolver
    {
        private readonly IReadOnlyDictionary<AssetSource, IJsCodeProvider> providers;

        public JsCodeResolver(IWebRequestController webRequestController)
        {
            providers = new Dictionary<AssetSource, IJsCodeProvider>
            {
                { AssetSource.WEB, new WebJsCodeProvider(webRequestController) },
            };
        }

        public UniTask<string> GetCodeContent(Uri contentUrl, CancellationToken ct) =>
            providers[AssetSource.WEB].GetJsCodeAsync(contentUrl, ct);
    }
}
