using Cysharp.Threading.Tasks;
using DCL.Browser;
using SceneRuntime.Apis.Modules;

namespace CrdtEcsBridge.RestrictedActions
{
    public class RestrictedActionsAPIImplementation : IRestrictedActionsAPI
    {
        private readonly IWebBrowser webBrowser;

        public RestrictedActionsAPIImplementation(IWebBrowser webBrowser)
        {
            this.webBrowser = webBrowser;
        }

        public bool OpenExternalUrl(string url)
        {
            OpenUrlAsync(url).Forget();
            return true;
        }

        private async UniTask OpenUrlAsync(string url)
        {
            await UniTask.SwitchToMainThread();
            webBrowser.OpenUrl(url);
        }

        public void Dispose() { }
    }
}
