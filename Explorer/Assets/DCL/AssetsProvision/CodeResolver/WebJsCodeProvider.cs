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
            GenericGetRequest rqs = await webRequestController.GetAsync(new CommonArguments(url), cancellationToken);
            string text = rqs.UnityWebRequest.downloadHandler.text;
            rqs.UnityWebRequest.Dispose();
            return text;
        }
    }
}
