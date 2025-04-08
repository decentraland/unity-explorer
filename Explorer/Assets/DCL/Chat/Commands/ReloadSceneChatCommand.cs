using Arch.Core;
using System.Threading;
using Cysharp.Threading.Tasks;
using ECS.SceneLifeCycle;

namespace DCL.Chat.Commands
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
        private readonly World globalWorld;
        private readonly Entity playerEntity;

        public ReloadSceneChatCommand(ECSReloadScene reloadScene, World globalWorld, Entity playerEntity)
        {
            this.reloadScene = reloadScene;
            this.globalWorld = globalWorld;
            this.playerEntity = playerEntity;
        }

        public async UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            globalWorld.Add<SceneReloadComponent>(playerEntity);

            if (await reloadScene.TryReloadSceneAsync(ct))
                return "🟢 Current scene has been reloaded";

            globalWorld.Remove<SceneReloadComponent>(playerEntity);

            return "🔴 You need to be in a SDK7 scene to reload it.";
        }

        public struct SceneReloadComponent {}
    }
}
