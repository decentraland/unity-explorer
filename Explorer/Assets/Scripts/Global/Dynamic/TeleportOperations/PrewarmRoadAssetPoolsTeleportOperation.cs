using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.LOD;

namespace Global.Dynamic.TeleportOperations
{
    public class PrewarmRoadAssetPoolsTeleportOperation : TeleportOperationBase
    {
        private readonly IGlobalRealmController realmController;
        private readonly RoadAssetsPool roadAssetsPool;

        public PrewarmRoadAssetPoolsTeleportOperation(IGlobalRealmController realmController, RoadAssetsPool roadAssetsPool)
        {
            this.roadAssetsPool = roadAssetsPool;
            this.realmController = realmController;
        }

        protected override UniTask ExecuteAsyncInternal(TeleportParams teleportParams, CancellationToken ct)
        {
            if(!realmController.RealmData.ScenesAreFixed) // Is Genesis
                roadAssetsPool.Prewarm();

            return UniTask.CompletedTask;
        }
    }
}
