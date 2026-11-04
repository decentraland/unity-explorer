using Cysharp.Threading.Tasks;
using DCL.LOD;
using ECS.SceneLifeCycle.Realm;
using ECS.SceneLifeCycle.SingleScene;
using System.Threading;

namespace DCL.RealmNavigation.TeleportOperations
{
    public class PrewarmRoadAssetPoolsTeleportOperation : TeleportOperationBase
    {
        private readonly IRealmController realmController;
        private readonly IRoadAssetPool roadAssetsPool;
        private readonly SingleSceneMode singleSceneMode;

        public PrewarmRoadAssetPoolsTeleportOperation(IRealmController realmController, IRoadAssetPool roadAssetsPool, SingleSceneMode singleSceneMode)
        {
            this.roadAssetsPool = roadAssetsPool;
            this.realmController = realmController;
            this.singleSceneMode = singleSceneMode;
        }

        protected override UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            if (singleSceneMode.IsActive) return UniTask.CompletedTask;

            // A competing realm change can leave RealmData unconfigured here, where ScenesAreFixed throws, and prewarming is optional (#10031).
            if (realmController.RealmData.Configured && !realmController.RealmData.ScenesAreFixed) // Is Genesis
                roadAssetsPool.Prewarm();

            return UniTask.CompletedTask;
        }
    }
}
