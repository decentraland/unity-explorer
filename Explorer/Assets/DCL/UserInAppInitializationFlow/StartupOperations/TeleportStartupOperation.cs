using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.RealmNavigation;
using DCL.RealmNavigation.TeleportOperations;
using DCL.Utilities;
using DCL.Utility.Types;
using ECS;
using ECS.Prioritization.Components;
using System.Threading;
using UnityEngine;

namespace DCL.UserInAppInitializationFlow
{
    public class TeleportStartupOperation : TeleportToSpawnPointOperationBase<IStartupOperation.Params>, IStartupOperation
    {
        private readonly StartParcel startParcel;

        public TeleportStartupOperation(
            ILoadingStatus loadingStatus,
            IGlobalRealmController realmController,
            ObjectProxy<Entity> cameraEntity,
            ITeleportController teleportController,
            CameraSamplingData cameraSamplingData, StartParcel startParcel, string reportCategory = ReportCategory.SCENE_LOADING)
            : base(loadingStatus, realmController, cameraEntity, teleportController, cameraSamplingData, reportCategory)
        {
            this.startParcel = startParcel;
        }

        public override UniTask<EnumResult<TaskError>> ExecuteAsync(IStartupOperation.Params args, CancellationToken ct)
        {
            // If the WorldManifest defines an explicit spawn coordinate, always use it
            // (e.g. worlds with a fixed or curated spawn point)
            WorldManifest manifest = realmController.RealmData.WorldManifest;
            if (manifest is { IsEmpty: false, spawn_coordinate: { } spawn })
                return InternalExecuteAsync(args, new Vector2Int(spawn.x, spawn.y), ct);

            return InternalExecuteAsync(args, startParcel.ConsumeByTeleportOperation(), ct);

        }
    }
}
