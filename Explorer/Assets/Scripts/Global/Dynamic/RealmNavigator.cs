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
        private readonly URLDomain genesisDomain = URLDomain.FromString(IRealmNavigator.GENESIS_URL);

        private readonly MVCManager mvcManager;
        private readonly IRealmController realmController;
        private readonly ITeleportController teleportController;

        public RealmNavigator(MVCManager mvcManager, IRealmController realmController, ITeleportController teleportController)
        {
            this.mvcManager = mvcManager;
            this.realmController = realmController;
            this.teleportController = teleportController;
        }

        public async UniTask<bool> TryChangeRealmAsync(string realm, CancellationToken ct)
        {
            var domain = URLDomain.FromString(realm);

            if (!await realmController.IsReachableAsync(domain, ct))
            {
                Debug.Log("VVV NOT REACHABLE!");
                return false;
            }

            await ShowLoadingScreenAndExecuteTaskAsync(loadReport =>
                realmController.SetRealmAsync(domain, Vector2Int.zero, loadReport, ct), ct);

            return true;
        }

        public async UniTask TeleportToParcelAsync(Vector2Int parcel, CancellationToken ct)
        {
            await ShowLoadingScreenAndExecuteTaskAsync(async loadReport =>
            {
                if (realmController.GetRealm().Ipfs.CatalystBaseUrl != genesisDomain)
                    await realmController.SetRealmAsync(genesisDomain, Vector2Int.zero, loadReport, ct);

                WaitForSceneReadiness? waitForSceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(parcel, loadReport, ct);
                await waitForSceneReadiness.ToUniTask(ct);
            }, ct);
        }

        private async UniTask ShowLoadingScreenAndExecuteTaskAsync(Func<AsyncLoadProcessReport, UniTask> operation, CancellationToken ct)
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
