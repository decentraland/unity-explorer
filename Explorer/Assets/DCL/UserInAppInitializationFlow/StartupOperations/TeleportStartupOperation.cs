using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.RealmNavigation;
using DCL.RealmNavigation.TeleportOperations;
using DCL.Utilities;
using DCL.Utility.Types;
using ECS;
using ECS.Prioritization.Components;
using Global.AppArgs;
using System.Threading;
using UnityEngine;

namespace DCL.UserInAppInitializationFlow
{
    public class TeleportStartupOperation : TeleportToSpawnPointOperationBase<IStartupOperation.Params>, IStartupOperation
    {
        private readonly StartParcel startParcel;
        private readonly IAppArgs appArgs;
        private readonly bool editorPositionOverrideActive;

        public TeleportStartupOperation(
            ILoadingStatus loadingStatus,
            IGlobalRealmController realmController,
            ObjectProxy<Entity> cameraEntity,
            ITeleportController teleportController,
            CameraSamplingData cameraSamplingData,
            StartParcel startParcel,
            IAppArgs appArgs,
            bool editorPositionOverrideActive = false,
            string reportCategory = ReportCategory.SCENE_LOADING)
            : base(loadingStatus, realmController, cameraEntity, teleportController, cameraSamplingData, reportCategory)
        {
            this.startParcel = startParcel;
            this.appArgs = appArgs;
            this.editorPositionOverrideActive = editorPositionOverrideActive;
        }

        public override async UniTask<EnumResult<TaskError>> ExecuteAsync(IStartupOperation.Params args, CancellationToken ct)
        {
            // --position flag or Editor Start Position take highest priority over any world manifest spawn coordinate
            if (!appArgs.HasFlag(AppArgsFlags.POSITION) && !editorPositionOverrideActive)
            {
                // If the WorldManifest defines an explicit spawn coordinate, use it
                // (e.g. worlds with a fixed or curated spawn point)
                WorldManifest manifest = realmController.RealmData.WorldManifest;
                if (manifest is { IsEmpty: false, spawn_coordinate: { } spawn })
                    return await InternalExecuteAsync(args, new Vector2Int(spawn.x, spawn.y), ct);

                // In local scene development, use the scene's base parcel so the player
                // spawns at the scene-defined spawn point instead of the editor targetScene
                if (realmController.RealmData.IsLocalSceneDevelopment)
                {
                    SceneDefinitions? sceneDefinitions = await realmController.WaitForStaticScenesEntityDefinitionsAsync(ct);

                    if (sceneDefinitions is { Value: { Count: > 0 } })
                        return await InternalExecuteAsync(args, sceneDefinitions.Value.Value[0].metadata.scene.DecodedBase, ct);
                }
            }

            return await InternalExecuteAsync(args, startParcel.ConsumeByTeleportOperation(), ct);
        }
    }
}
