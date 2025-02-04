using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;

namespace ECS.StreamableLoading.DeferredLoading
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public class QualityReductorSystem : BaseUnityLoopSystem
    {
        public QualityReductorSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            ProcessQualityReductionQuery(World);
        }

        [Query]
        public void ProcessQualityReduction(in QualityReductionRequest qualityReductionRequest) { }
    }
}
