using AssetManagement;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AssetsProvision.CodeResolver
{
    public class JsCodeResolver
    {
        private readonly IReadOnlyDictionary<AssetSource, IJsCodeProvider> providers = new Dictionary<AssetSource, IJsCodeProvider>
        {
            { AssetSource.WEB, new WebJsCodeProvider() },
        };

        public UniTask<string> GetCodeContent(string contentUrl, CancellationToken ct) =>
            providers[AssetSource.WEB].GetJsCodeAsync(contentUrl, ct);
    }
}
