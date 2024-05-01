using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using System.Threading;

namespace DCL.AssetsProvision.CodeResolver
{
    public class WebJsCodeProvider : IJsCodeProvider
    {
        private readonly IWebRequestController webRequestController;

        public WebJsCodeProvider(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public async UniTask<string> GetJsCodeAsync(URLAddress url, CancellationToken cancellationToken = default)
        {
            string text = await webRequestController.GetAsync(new CommonArguments(url), cancellationToken).StoreTextAsync();
            return text;
        }
    }
}
