using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.ParcelsService;
using DCL.SceneLoadingScreens;
using ECS.SceneLifeCycle.Realm;
using ECS.SceneLifeCycle.Reporting;
using MVC;
using System;
using System.Threading;
using UnityEngine;

namespace Global.Dynamic
{
    public class RealmNavigator : IRealmNavigator
    {
        private readonly MVCManager mvcManager;
        private readonly IRealmController realmController;
        private readonly ITeleportController teleportController;

        public RealmNavigator(MVCManager mvcManager, IRealmController realmController, ITeleportController teleportController)
        {
            this.mvcManager = mvcManager;
            this.realmController = realmController;
            this.teleportController = teleportController;
        }

        public async UniTask ChangeRealmAsync(string realm, CancellationToken ct)
        {
            await ShowLoadingScreenAndExecuteTask(loadReport =>
                realmController.SetRealmAsync(URLDomain.FromString(realm), Vector2Int.zero, loadReport, ct), ct);
        }

        public async UniTask TeleportToParcel(Vector2Int parcel, CancellationToken ct)
        {
            await ShowLoadingScreenAndExecuteTask(async loadReport =>
            {
                var waitForSceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(parcel, loadReport, ct);
                await waitForSceneReadiness.ToUniTask(ct);
            }, ct);
        }

        private async UniTask ShowLoadingScreenAndExecuteTask(Func<AsyncLoadProcessReport, UniTask> operation, CancellationToken ct)
        {
            var timeout = TimeSpan.FromSeconds(30);
            var loadReport = AsyncLoadProcessReport.Create();

            UniTask showLoadingScreenTask = mvcManager.ShowAsync(
                                                           SceneLoadingScreenController.IssueCommand(
                                                               new SceneLoadingScreenController.Params(loadReport, timeout)), ct)
                                                      .AttachExternalCancellation(ct);

            UniTask operationTask = operation(loadReport);

            await UniTask.WhenAll(showLoadingScreenTask, operationTask);
        }
    }
}
