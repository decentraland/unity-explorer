using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using ECS.SceneLifeCycle;
using System;
using System.Threading;
using UnityEngine;

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
        private readonly bool isLocalSceneDevelopmentMode;

        private readonly Func<bool> sceneReadyCondition;

        public ReloadSceneChatCommand(ECSReloadScene reloadScene,
            World globalWorld,
            Entity playerEntity,
            IScenesCache scenesCache,
            bool isLocalSceneDevelopmentMode)
        {
            this.reloadScene = reloadScene;
            this.globalWorld = globalWorld;
            this.playerEntity = playerEntity;
            this.isLocalSceneDevelopmentMode = isLocalSceneDevelopmentMode;

            this.sceneReadyCondition = () => scenesCache.CurrentScene != null && scenesCache.CurrentScene.IsSceneReady();
        }

        public async UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            if (isLocalSceneDevelopmentMode)
                globalWorld.Add<StopCharacterMotion>(playerEntity);

            try
            {
                var reloadedScene = await reloadScene.TryReloadSceneAsync(ct);

                await UniTask.WaitUntil(sceneReadyCondition, cancellationToken: ct);

                if (!isLocalSceneDevelopmentMode)
                {
                    if (reloadedScene != null)
                    {
                        globalWorld.AddOrGet(playerEntity,
                            new PlayerTeleportIntent(reloadedScene.SceneData.SceneEntityDefinition,
                                reloadedScene.SceneData.SceneShortInfo.BaseParcel, Vector3.zero, ct));
                    }
                }

                return reloadedScene != null
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
