using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.SceneLoadingScreens;
using Global.Dynamic;
using MVC;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.RealmNavigator
{
    public class RealmNavigationService
    {
        private readonly MVCManager mvcManager;
        private readonly IRealmController realmController;

        public RealmNavigationService(MVCManager mvcManager, IRealmController realmController)
        {
            this.mvcManager = mvcManager;
            this.realmController = realmController;
        }

        public async UniTask ChangeRealmAsync(string realm, CancellationToken ct)
        {
            var loadReport = new AsyncLoadProcessReport(new UniTaskCompletionSource(), new AsyncReactiveProperty<float>(0));

            await UniTask.WhenAll(
                mvcManager.ShowAsync(SceneLoadingScreenController.IssueCommand(new SceneLoadingScreenController.Params(loadReport, TimeSpan.FromSeconds(30))), ct),
                realmController.SetRealmAsync(URLDomain.FromString(realm), Vector2Int.zero, loadReport, ct)
            );
        }

    }
}
