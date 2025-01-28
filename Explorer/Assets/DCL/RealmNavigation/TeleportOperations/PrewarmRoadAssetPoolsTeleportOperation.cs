using Cysharp.Threading.Tasks;
using DCL.LOD;
using ECS.SceneLifeCycle.Realm;
using System.Threading;

namespace DCL.RealmNavigation.TeleportOperations
{
    public class PrewarmRoadAssetPoolsTeleportOperation : TeleportOperationBase
    {
        private readonly IRealmController realmController;
        private readonly RoadAssetsPool roadAssetsPool;

        public PrewarmRoadAssetPoolsTeleportOperation(IRealmController realmController, RoadAssetsPool roadAssetsPool)
        {
            this.roadAssetsPool = roadAssetsPool;
            this.realmController = realmController;
        }

        protected override UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            if(!realmController.RealmData.ScenesAreFixed) // Is Genesis
                roadAssetsPool.Prewarm();

            return UniTask.CompletedTask;
        }
    }
}
