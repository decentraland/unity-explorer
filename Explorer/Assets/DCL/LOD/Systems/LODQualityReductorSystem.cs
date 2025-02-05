using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using DCL.LOD.Components;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using ECS.StreamableLoading.DeferredLoading.Components;

namespace DCL.LOD.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [LogCategory(ReportCategory.LOD)]
    public partial class LODQualityReductorSystem : BaseUnityLoopSystem
    {
        private static readonly QueryDescription ALL_LOD_QUERY_FULL_QUALITY = new QueryDescription()
            .WithAll<SceneLODInfo>()
            .WithNone<LODQualityReducedComponent>();

        private static readonly QueryDescription ALL_LOD_QUERY_REDUCED_QUALITY = new QueryDescription()
            .WithAll<SceneLODInfo, LODQualityReducedComponent>();


        private readonly ILODCache lodCache;


        public LODQualityReductorSystem(World world, ILODCache lodCache) : base(world)
        {
            this.lodCache = lodCache;
        }


        protected override void Update(float t)
        {
            TryReduceQualityQuery(World);
            TryIncreaseQualityQuery(World);
        }

        [Query]
        private void TryReduceQuality(in Entity entity, ref QualityChangeRequest reductionRequest)
        {
            if (reductionRequest.Domain != QualityReductionRequestDomain.LOD) return;

            if (!reductionRequest.IsReduce()) return;

            //TODO: Use frame budget?
            World.Query(ALL_LOD_QUERY_FULL_QUALITY, (Entity entity, ref SceneLODInfo sceneLODInfo) =>
            {
                ReduceLODQuality(ref sceneLODInfo);
                World.Add(entity, new LODQualityReducedComponent());
            });
            World.Destroy(entity);
        }


        [Query]
        private void TryIncreaseQuality(in Entity entity, ref QualityChangeRequest reductionRequest)
        {
            if (reductionRequest.Domain != QualityReductionRequestDomain.LOD) return;

            if (reductionRequest.IsReduce()) return;

            //TODO: Use frame budget?
            World.Query(ALL_LOD_QUERY_REDUCED_QUALITY, (Entity entity, ref SceneLODInfo sceneLODInfo) =>
            {
                sceneLODInfo.metadata.Reset();
                World.Remove<LODQualityReducedComponent>(entity);
            });
            World.Destroy(entity);
        }

        private void ReduceLODQuality(ref SceneLODInfo sceneLODInfo)
        {
            if (!sceneLODInfo.IsInitialized())
                return;
            
            lodCache.Release(sceneLODInfo.id, sceneLODInfo.metadata);
            sceneLODInfo.metadata.SuccessfullLODs = 0;
            sceneLODInfo.metadata.FailedLODs = 0;
            sceneLODInfo.Dispose(World);
        }
    }
}