using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.ParcelsService;
using DCL.RealmNavigation.LoadingOperation;
using DCL.UserInAppInitializationFlow;
using DCL.Utilities;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Realm;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using Global.Dynamic;
using NUnit.Framework;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.RealmNavigation.TeleportOperations
{
    public abstract class TeleportToSpawnPointOperationBase<TParams> : LoadingOperationBase<TParams> where TParams: ILoadingOperationParams
    {
        private readonly ILoadingStatus loadingStatus;
        private readonly IGlobalRealmController realmController;
        private readonly ObjectProxy<Entity> cameraEntity;
        private readonly CameraSamplingData cameraSamplingData;
        private readonly ITeleportController teleportController;

        protected TeleportToSpawnPointOperationBase(ILoadingStatus loadingStatus, IGlobalRealmController realmController, ObjectProxy<Entity> cameraEntity, ITeleportController teleportController, CameraSamplingData cameraSamplingData,
            string reportCategory = ReportCategory.SCENE_LOADING) : base(reportCategory)
        {
            this.loadingStatus = loadingStatus;
            this.realmController = realmController;
            this.cameraEntity = cameraEntity;
            this.teleportController = teleportController;
            this.cameraSamplingData = cameraSamplingData;
        }

        protected async UniTask InternalExecuteAsync(TParams args, Vector2Int parcel, CancellationToken ct)
        {
            float finalizationProgress = loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.PlayerTeleporting);
            AsyncLoadProcessReport teleportLoadReport = args.Report.CreateChildReport(finalizationProgress);
            await InitializeTeleportToSpawnPointAsync(teleportLoadReport, ct, parcel);
            args.Report.SetProgress(finalizationProgress);
        }

        private async UniTask InitializeTeleportToSpawnPointAsync(
            AsyncLoadProcessReport teleportLoadReport,
            CancellationToken ct,
            Vector2Int parcelToTeleport
        )
        {
            bool isWorld = realmController.Type is RealmType.World;
            WaitForSceneReadiness? waitForSceneReadiness;

            if (isWorld)
                waitForSceneReadiness = await TeleportToWorldSpawnPointAsync(parcelToTeleport, teleportLoadReport, ct);
            else
                waitForSceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(parcelToTeleport, teleportLoadReport, ct);

            // add camera sampling data to the camera entity to start partitioning
            Assert.IsTrue(cameraEntity.Configured);
            realmController.GlobalWorld.EcsWorld.Add(cameraEntity.Object, cameraSamplingData);
            await waitForSceneReadiness.ToUniTask();
        }

        private async UniTask<WaitForSceneReadiness?> TeleportToWorldSpawnPointAsync(
            Vector2Int parcelToTeleport,
            AsyncLoadProcessReport processReport,
            CancellationToken ct
        )
        {
            AssetPromise<SceneEntityDefinition, GetSceneDefinition>[]? promises = await realmController.WaitForFixedScenePromisesAsync(ct);

            if (!promises.Any(p =>
                    p.Result.HasValue
                    && (p.Result.Value.Asset?.metadata.scene.DecodedParcels.Contains(parcelToTeleport) ?? false)
                ))
                parcelToTeleport = promises[0].Result!.Value.Asset!.metadata.scene.DecodedBase;

            WaitForSceneReadiness? waitForSceneReadiness =
                await teleportController.TeleportToSceneSpawnPointAsync(parcelToTeleport, processReport, ct);

            return waitForSceneReadiness;
        }
    }
}
