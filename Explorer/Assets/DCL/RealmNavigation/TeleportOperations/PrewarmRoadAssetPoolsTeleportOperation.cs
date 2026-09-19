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
            // RealmData can be transiently unconfigured here: a competing realm change invalidates it
            // between this pipeline's ChangeRealmTeleportOperation and this step, and ScenesAreFixed
            // throws on an unconfigured realm. Prewarming is an optional optimization (road pools warm
            // lazily on demand), so it must never fail the whole teleport sequence over that window;
            // operations that genuinely require the realm still fail loudly on their own.
            if (realmController.RealmData.Configured && !realmController.RealmData.ScenesAreFixed) // Is Genesis
                roadAssetsPool.Prewarm();

            return UniTask.CompletedTask;
        }
    }
}
