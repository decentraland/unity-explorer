using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities;
using DCL.Utility.Types;
using ECS;
using ECS.Prioritization.Components;
using System.Threading;
using UnityEngine;

namespace DCL.RealmNavigation.TeleportOperations
{
    public class MoveToParcelInNewRealmTeleportOperation : TeleportToSpawnPointOperationBase<TeleportParams>, ITeleportOperation
    {
        public MoveToParcelInNewRealmTeleportOperation(ILoadingStatus loadingStatus, IGlobalRealmController realmController, ObjectProxy<Entity> cameraEntity, ITeleportController teleportController, CameraSamplingData cameraSamplingData,
            string reportCategory = ReportCategory.SCENE_LOADING) : base(loadingStatus, realmController, cameraEntity, teleportController, cameraSamplingData, reportCategory) { }

        public override UniTask<EnumResult<TaskError>> ExecuteAsync(TeleportParams args, CancellationToken ct)
        {
            // If the WorldManifest defines an explicit spawn coordinate, use it when its allowed by the teleport params
            // (e.g. worlds with a fixed or curated spawn point)
            WorldManifest manifest = realmController.RealmData.WorldManifest;
            if (args.AllowsWorldPositionOverride && manifest is { IsEmpty: false, spawn_coordinate: { } spawn })
                args = new TeleportParams(args.CurrentDestinationRealm, new Vector2Int(spawn.x, spawn.y), args.Report, args.LoadingStatus, args.AllowsWorldPositionOverride );

            return InternalExecuteAsync(args, args.CurrentDestinationParcel, ct);
        }
    }
}
