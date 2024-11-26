using System.Threading;
using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.LOD;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Roads.Components;
using System;
using Utility.Types;

namespace Global.Dynamic.TeleportOperations
{
    public class DestroyAllRoadAssetsTeleportOperation : TeleportOperationBase
    {
        private readonly World globalWorld;
        private readonly RoadAssetsPool roadAssetsPool;
        private readonly IPerformanceBudget unlimitedFPSBudget;

        public DestroyAllRoadAssetsTeleportOperation(World globalWorld, RoadAssetsPool roadAssetsPool)
        {
            this.globalWorld = globalWorld;
            this.roadAssetsPool = roadAssetsPool;
            unlimitedFPSBudget = new NullPerformanceBudget();
        }

        protected override UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            globalWorld.Query(new QueryDescription().WithAll<RoadInfo>(), entity => globalWorld.Get<RoadInfo>(entity).Dispose(roadAssetsPool));
            roadAssetsPool.Unload(unlimitedFPSBudget, int.MaxValue);

            return UniTask.CompletedTask;
        }
    }
}
