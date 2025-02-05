using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.RealmNavigation;
using DCL.RealmNavigation.TeleportOperations;
using DCL.Utilities;
using ECS.Prioritization.Components;
using Global.Dynamic;
using System;
using System.Threading;
using UnityEngine;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class TeleportStartupOperation : TeleportToSpawnPointOperationBase<IStartupOperation.Params>, IStartupOperation
    {
        private readonly Vector2Int startParcel;

        public TeleportStartupOperation(
            ILoadingStatus loadingStatus,
            IGlobalRealmController realmController,
            ObjectProxy<Entity> cameraEntity,
            ITeleportController teleportController,
            CameraSamplingData cameraSamplingData, Vector2Int startParcel, string reportCategory = ReportCategory.SCENE_LOADING)
            : base(loadingStatus, realmController, cameraEntity, teleportController, cameraSamplingData, reportCategory)
        {
            this.startParcel = startParcel;
        }

        public override UniTask<EnumResult<TaskError>> ExecuteAsync(IStartupOperation.Params args, CancellationToken ct) =>
            InternalExecuteAsync(args, startParcel, ct);
    }
}
