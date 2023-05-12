using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace AssetManagement.JsCodeResolver
{
    public class JsCodeResolver
    {
        private readonly IReadOnlyDictionary<AssetSource, IJsCodeProvider> providers;

        public JsCodeResolver()
        {
            providers = new Dictionary<AssetSource, IJsCodeProvider>
            {
                { AssetSource.WEB, new WebJsCodeProvider() },
            };
        }

        public UniTask<string> GetCodeContent(string contentUrl, CancellationToken ct) =>
            providers[AssetSource.WEB].GetJsCodeAsync(contentUrl, ct);
    }
}
