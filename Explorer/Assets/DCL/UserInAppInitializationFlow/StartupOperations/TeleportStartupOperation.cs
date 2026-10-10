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
            string? spawnPointName = startParcel.SpawnPointName;
            Vector2Int destination = await ResolveDestinationAsync(ct);

            // Consumed on every path: a consumed start parcel is the mark that the startup teleport happened
            startParcel.ConsumeByTeleportOperation();

            return await InternalExecuteAsync(args, destination, ct, spawnPointName: spawnPointName);
        }

        private async UniTask<Vector2Int> ResolveDestinationAsync(CancellationToken ct)
        {
            // A picked realm's manifest spawn wins over the launch --position and the Editor override; the launch value stays the last fallback
            bool useDefault = startParcel.IsParcelAssigned
                              || (startParcel.Realm == null && (appArgs.HasFlag(AppArgsFlags.POSITION) || editorPositionOverrideActive));

            if (useDefault)
                return startParcel.Peek();

            // World manifest spawn coordinate takes next priority
            if (realmController.RealmData.WorldManifest is { IsEmpty: false, spawn_coordinate: { } spawn })
                return new Vector2Int(spawn.x, spawn.y);

            // Local scene development: use the scene's base parcel as spawn point
            return realmController.RealmData.IsLocalSceneDevelopment
                   && await realmController.WaitForStaticScenesEntityDefinitionsAsync(ct) is { Value: { Count: > 0 } } sceneDefinitions
                ? sceneDefinitions.Value[0].metadata.scene.DecodedBase
                : startParcel.Peek();
        }
    }
}
