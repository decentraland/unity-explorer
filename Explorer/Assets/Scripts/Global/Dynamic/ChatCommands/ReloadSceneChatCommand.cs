using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using ECS.SceneLifeCycle;

namespace Global.Dynamic.ChatCommands
{
    /// <summary>
    /// Reloads the current scene.
    ///
    /// Usage:
    ///     /reload
    /// </summary>
    public class ReloadSceneChatCommand : IChatCommand
    {
        public string Command => "reload";
        public string Description => "<b>/reload </b>\n  Reload the current scene";

        private readonly ECSReloadScene reloadScene;

        public ReloadSceneChatCommand(ECSReloadScene reloadScene)
        {
            this.reloadScene = reloadScene;
        }

        public async UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            if (await reloadScene.TryReloadSceneAsync(ct))
                return "🟢 Current scene has been reloaded";

            return "🔴 You need to be in a SDK7 scene to reload it.";
        }
    }
}
