using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat;
using ECS.SceneLifeCycle.Systems;

namespace Global.Dynamic.ChatCommands
{
    public class ReloadSceneChatCommand : IChatCommand
    {
        public static readonly Regex REGEX = new (@"^/reload(?:\s+(\w+))?$", RegexOptions.Compiled);

        private readonly ReloadSceneController reloadSceneController;

        public ReloadSceneChatCommand(ReloadSceneController reloadSceneController)
        {
            this.reloadSceneController = reloadSceneController;
        }

        public async UniTask<string> ExecuteAsync(Match match, CancellationToken ct)
        {
            if (await reloadSceneController.TryReloadSceneAsync())
                return "\ud83d\udfe2 Current scene has been reloaded";
            return "\ud83d\udd34 You need to be in a SDK7 scene to reload it.";
        }
    }
}