using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.ExternalUrlPrompt;
using MVC;
using SceneRunner.Scene;
using SceneRuntime.Apis.Modules;

namespace CrdtEcsBridge.RestrictedActions
{
    public class RestrictedActionsAPIImplementation : IRestrictedActionsAPI
    {
        private readonly IMVCManager mvcManager;
        private readonly ISceneStateProvider sceneStateProvider;

        public RestrictedActionsAPIImplementation(IMVCManager mvcManager, ISceneStateProvider sceneStateProvider)
        {
            this.mvcManager = mvcManager;
            this.sceneStateProvider = sceneStateProvider;
        }

        public bool OpenExternalUrl(string url)
        {
            if (!sceneStateProvider.IsCurrent)
            {
                ReportHub.LogError(ReportCategory.RESTRICTED_ACTIONS, "Error: Player is not inside of scene");
                return false;
            }

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
