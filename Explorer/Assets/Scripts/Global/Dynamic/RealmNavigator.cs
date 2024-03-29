// using CommunicationData.URLHelpers;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.ParcelsService;
using DCL.SceneLoadingScreens;
using ECS.SceneLifeCycle.Reporting;
using Global.Dynamic;
using MVC;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.RealmNavigation
{
    public class RealmNavigator
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
            var loadReport = new AsyncLoadProcessReport(new UniTaskCompletionSource(), new AsyncReactiveProperty<float>(0));

            await UniTask.WhenAll(
                mvcManager.ShowAsync(SceneLoadingScreenController.IssueCommand(new SceneLoadingScreenController.Params(loadReport, TimeSpan.FromSeconds(30))), ct),
                realmController.SetRealmAsync(URLDomain.FromString(realm), Vector2Int.zero, loadReport, ct)
            );
        }

        public async UniTask TeleportToParcel(Vector2Int parcel, CancellationToken ct)
        {
            var timeout = TimeSpan.FromSeconds(30);
            var loadReport = AsyncLoadProcessReport.Create();

            await UniTask.WhenAll(
                mvcManager.ShowAsync(SceneLoadingScreenController.IssueCommand(new SceneLoadingScreenController.Params(loadReport!, timeout)), ct).AttachExternalCancellation(ct),
                TeleportAsync()
            );

            return;

            async UniTask TeleportAsync()
            {
                WaitForSceneReadiness waitForSceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(parcel, loadReport, ct);
                await waitForSceneReadiness.ToUniTask(ct);
            }
        }
    }
}
