using Arch.Core;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Character.CharacterMotion.Components;
using ECS.SceneLifeCycle;
using System;
using DCL.Chat.History;

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

        private readonly Func<bool> sceneReadyCondition;

        public ReloadSceneChatCommand(ECSReloadScene reloadScene, World globalWorld, Entity playerEntity, IScenesCache scenesCache)
        {
            this.reloadScene = reloadScene;
            this.globalWorld = globalWorld;
            this.playerEntity = playerEntity;

            this.sceneReadyCondition = () => scenesCache.CurrentScene != null && scenesCache.CurrentScene.IsSceneReady();
        }

        public async UniTask<string> ExecuteCommandAsync(ChatChannel channel, string[] parameters, CancellationToken ct)
        {
            globalWorld.Add<StopCharacterMotion>(playerEntity);

            try
            {
                bool isSuccess = await reloadScene.TryReloadSceneAsync(ct);
                await UniTask.WaitUntil(sceneReadyCondition, cancellationToken: ct);
                return isSuccess
                    ? "🟢 Current scene has been reloaded"
                    : "🔴 You need to be in a SDK7 scene to reload it.";
            }
            finally
            {
                globalWorld.Remove<StopCharacterMotion>(playerEntity);
            }
        }
    }
}
