using Cysharp.Threading.Tasks;
using DCL.ExternalUrlPrompt;
using MVC;
using SceneRuntime.Apis.Modules;

namespace CrdtEcsBridge.RestrictedActions
{
    public class RestrictedActionsAPIImplementation : IRestrictedActionsAPI
    {
        private readonly IMVCManager mvcManager;

        public RestrictedActionsAPIImplementation(IMVCManager mvcManager)
        {
            this.mvcManager = mvcManager;
        }

        public bool OpenExternalUrl(string url)
        {
            OpenUrlAsync(url).Forget();
            return true;
        }

        private async UniTask OpenUrlAsync(string url)
        {
            await UniTask.SwitchToMainThread();
            await mvcManager.ShowAsync(ExternalUrlPromptController.IssueCommand(new ExternalUrlPromptController.Params(url)));
        }

        public void Dispose() { }
    }
}
