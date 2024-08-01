using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using ECS.SceneLifeCycle.Realm;
using System.Threading;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class SwitchRealmMiscVisibilityStartupOperation : IStartupOperation
    {
        private readonly IRealmNavigator realmNavigator;

        public SwitchRealmMiscVisibilityStartupOperation(IRealmNavigator realmNavigator)
        {
            this.realmNavigator = realmNavigator;
        }

        public async UniTask<StartupResult> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            await realmNavigator.SwitchMiscVisibilityAsync();
            return StartupResult.SuccessResult();
        }
    }
}
