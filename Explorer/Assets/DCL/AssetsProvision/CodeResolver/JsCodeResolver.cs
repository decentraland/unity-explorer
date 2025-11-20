using AssetManagement;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Utility.Types;
using DCL.WebRequests;
using System.Collections.Generic;
using System.Threading;
using DownloadedCodeContent = UnityEngine.Networking.DownloadHandler;
// a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a
// a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a
// a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a aa

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

        public UniTask<Result<DownloadedCodeContent>> GetCodeContent(URLAddress contentUrl,
            CancellationToken ct) =>
            providers[AssetSource.WEB].GetJsCodeAsync(contentUrl, ct);
    }
}
