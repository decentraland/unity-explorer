using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Roads.Systems;
using Utility.Types;

namespace Global.Dynamic.TeleportOperations
{
    public class PrewarmRoadAssetPoolsTeleportOperation : ITeleportOperation
    {
        private readonly IGlobalRealmController realmController;
        private readonly RoadPlugin roadPlugin;

        public PrewarmRoadAssetPoolsTeleportOperation(IGlobalRealmController realmController, RoadPlugin roadPlugin)
        {
            this.roadPlugin = roadPlugin;
            this.realmController = realmController;
        }

        public UniTask<Result> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            // When Genesis is loaded, road asset pools must pre-allocate some instances to reduce allocations while playing
            if(!realmController.RealmData.ScenesAreFixed) // Is Genesis
                roadPlugin.RoadAssetPool.Prewarm();

            return UniTask.FromResult(Result.SuccessResult());
        }
    }
}
