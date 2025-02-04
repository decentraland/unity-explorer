using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;

namespace ECS.StreamableLoading.DeferredLoading
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class QualityReductorSystem : BaseUnityLoopSystem
    {
        public QualityReductorSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            ProcessQualityReductionQuery(World);
        }

        [Query]
        public void ProcessQualityReduction(in Entity entity, in QualityReductionRequest qualityReductionRequest)
        {
            World.Create(new AvatarQualityReductionRequest());
            World.Destroy(entity);
        }
    }
}
