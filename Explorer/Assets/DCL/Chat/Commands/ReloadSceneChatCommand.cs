using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.RealmNavigation;
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
        private readonly ITeleportController teleportController;
        private readonly bool isLocalSceneDevelopmentMode;

        private readonly Func<bool> sceneReadyCondition;

        public ReloadSceneChatCommand(ECSReloadScene reloadScene,
            World globalWorld,
            Entity playerEntity,
            IScenesCache scenesCache,
            ITeleportController teleportController,
            bool isLocalSceneDevelopmentMode)
        {
            this.reloadScene = reloadScene;
            this.globalWorld = globalWorld;
            this.playerEntity = playerEntity;
            this.teleportController = teleportController;
            this.isLocalSceneDevelopmentMode = isLocalSceneDevelopmentMode;

            this.sceneReadyCondition = () => scenesCache.CurrentScene.Value != null && scenesCache.CurrentScene.Value.IsSceneReady();
        }

        public async UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            if(globalWorld.Has<CharacterRigidTransform>(playerEntity))
                ResetExternalPhysics(globalWorld.Get<CharacterRigidTransform>(playerEntity));

            if (isLocalSceneDevelopmentMode)
                globalWorld.Add<StopCharacterMotion>(playerEntity);

            try
            {
                var reloadedScene = await reloadScene.TryReloadSceneAsync(ct);

                await UniTask.WaitUntil(sceneReadyCondition, cancellationToken: ct);

                if (!isLocalSceneDevelopmentMode && reloadedScene != null)
                    teleportController.StartTeleportToSpawnPoint(reloadedScene.SceneData.SceneEntityDefinition, ct);

                return reloadedScene != null
                    ? "🟢 Current scene has been reloaded"
                    : "🔴 You need to be in a SDK7 scene to reload it.";
            }
            finally
            {
                globalWorld.Remove<StopCharacterMotion>(playerEntity);
            }
        }

        private static void ResetExternalPhysics(CharacterRigidTransform rigidTransform)
        {
            rigidTransform.ExternalForce = Vector3.zero;
            rigidTransform.ExternalAcceleration = Vector3.zero;
            rigidTransform.ExternalImpulse = Vector3.zero;
            rigidTransform.ExternalVelocity = Vector3.zero;
        }
    }
}
