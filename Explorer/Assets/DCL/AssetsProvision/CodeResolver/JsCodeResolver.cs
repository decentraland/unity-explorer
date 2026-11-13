using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using System.Threading;

namespace DCL.AssetsProvision.CodeResolver
{
    public class JsCodeResolver
    {
        private readonly WebJsCodeProvider webProvider;

        public JsCodeResolver(IWebRequestController webRequestController)
        {
            webProvider = new WebJsCodeProvider(webRequestController);
        }

        public UniTask<string> GetCodeContent(URLAddress contentUrl, CancellationToken ct) =>
            webProvider.GetJsCodeAsync(contentUrl, ct);
    }
}
