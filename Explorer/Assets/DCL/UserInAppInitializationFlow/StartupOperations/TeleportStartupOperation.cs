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
            Vector2Int spawnParcel = startParcel.ConsumeByTeleportOperation();

            // In the editor, when previewing a local scene, ignore the editor start position override
            // so the scene's own spawn point is used. Builds launched via Creator Hub are not affected.
            bool editorOverride = editorPositionOverrideActive
                                  && !(realmController.RealmData.IsLocalSceneDevelopment && Application.isEditor);

            // --position flag or effective editor override → use default start parcel
            bool useDefault = appArgs.HasFlag(AppArgsFlags.POSITION) || editorOverride;

            if (useDefault)
                return await InternalExecuteAsync(args, spawnParcel, ct);

            // World manifest spawn coordinate takes next priority
            if (realmController.RealmData.WorldManifest is { IsEmpty: false, spawn_coordinate: { } spawn })
                return await InternalExecuteAsync(args, new Vector2Int(spawn.x, spawn.y), ct);

            // Local scene development: use the scene's base parcel as spawn point
            return realmController.RealmData.IsLocalSceneDevelopment
                   && await realmController.WaitForStaticScenesEntityDefinitionsAsync(ct) is { Value: { Count: > 0 } } sceneDefinitions
                ? await InternalExecuteAsync(args, sceneDefinitions.Value[0].metadata.scene.DecodedBase, ct)
                : await InternalExecuteAsync(args, spawnParcel, ct);
        }
    }
}
