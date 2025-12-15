using AssetManagement;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AssetsProvision.CodeResolver
{
    public class JsCodeResolver
    {
        private readonly IReadOnlyDictionary<AssetSource, WebJsCodeProvider> providers;

        public JsCodeResolver(IWebRequestController webRequestController)
        {
            providers = new Dictionary<AssetSource, WebJsCodeProvider>
            {
                { AssetSource.WEB, new WebJsCodeProvider(webRequestController) },
            };
        }

        public UniTask<string> GetCodeContent(URLAddress contentUrl, CancellationToken ct) =>
            providers[AssetSource.WEB].GetJsCodeAsync(contentUrl, ct);
    }
}
