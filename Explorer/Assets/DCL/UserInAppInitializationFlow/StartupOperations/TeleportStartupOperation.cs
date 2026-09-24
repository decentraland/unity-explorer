using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.RealmNavigation;
using DCL.RealmNavigation.TeleportOperations;
using DCL.Utilities;
using DCL.Utility.Types;
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
            IRoomHub roomHub,
            bool editorPositionOverrideActive = false,
            string reportCategory = ReportCategory.SCENE_LOADING)
            : base(loadingStatus, realmController, cameraEntity, teleportController, cameraSamplingData, roomHub, reportCategory)
        {
            this.startParcel = startParcel;
            this.appArgs = appArgs;
            this.editorPositionOverrideActive = editorPositionOverrideActive;
        }

        public override async UniTask<EnumResult<TaskError>> ExecuteAsync(IStartupOperation.Params args, CancellationToken ct)
        {
            // The Editor start position override is equivalent to passing --position: both win over the world
            // manifest spawn and over the local scene's base parcel.
            bool useDefault = appArgs.HasFlag(AppArgsFlags.POSITION) || editorPositionOverrideActive;

            string? spawnPointName = startParcel.SpawnPointName;

            if (useDefault)
                return await InternalExecuteAsync(args, startParcel.ConsumeByTeleportOperation(), ct, spawnPointName: spawnPointName);

            // World manifest spawn coordinate takes next priority
            if (realmController.RealmData.WorldManifest is { IsEmpty: false, spawn_coordinate: { } spawn })
                return await InternalExecuteAsync(args, new Vector2Int(spawn.x, spawn.y), ct, spawnPointName: spawnPointName);

            // Local scene development: use the scene's base parcel as spawn point
            return realmController.RealmData.IsLocalSceneDevelopment
                   && await realmController.WaitForStaticScenesEntityDefinitionsAsync(ct) is { Value: { Count: > 0 } } sceneDefinitions
                ? await InternalExecuteAsync(args, sceneDefinitions.Value[0].metadata.scene.DecodedBase, ct, spawnPointName: spawnPointName)
                : await InternalExecuteAsync(args, startParcel.ConsumeByTeleportOperation(), ct, spawnPointName: spawnPointName);
        }
    }
}
