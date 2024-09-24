using System.Threading;
using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Roads.Components;
using DCL.Roads.Systems;
using Utility.Types;

namespace Global.Dynamic.TeleportOperations
{
    public class DestroyAllRoadAssetsTeleportOperation : ITeleportOperation
    {
        private readonly World globalWorld;
        private readonly RoadPlugin roadPlugin;

        public DestroyAllRoadAssetsTeleportOperation(World globalWorld, RoadPlugin roadPlugin)
        {
            this.globalWorld = globalWorld;
            this.roadPlugin = roadPlugin;
        }

        public UniTask<Result> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            // Releases all the road assets, returning them to the pool and then destroys all the road assets
            globalWorld.Query(new QueryDescription().WithAll<RoadInfo>(), (entity) => globalWorld.Get<RoadInfo>(entity).Dispose(roadPlugin.RoadAssetPool));
            roadPlugin.RoadAssetPool.UnloadImmediate();

            return UniTask.FromResult(Result.SuccessResult());
        }
    }
}
