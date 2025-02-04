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
        private bool avatarQualityReduced;
        public QualityReductorSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            ProcessQualityReductionQuery(World);
            ProcessQualityIncreaseQuery(World);
        }

        [Query]
        public void ProcessQualityReduction(in Entity entity, in QualityReductionRequest qualityReductionRequest)
        {
            if (!qualityReductionRequest.Reduce)
                return;
            if (!avatarQualityReduced)
            {
                World.Create(new AvatarQualityReductionRequest(qualityReductionRequest.Reduce));
                avatarQualityReduced = true;
            }
            World.Destroy(entity);
        }

        [Query]
        public void ProcessQualityIncrease(in Entity entity, in QualityReductionRequest qualityReductionRequest)
        {
            if (qualityReductionRequest.Reduce)
                return;
            if (avatarQualityReduced)
            {
                World.Create(new AvatarQualityReductionRequest(qualityReductionRequest.Reduce));
                avatarQualityReduced = false;
            }

            World.Destroy(entity);
        }
    }
}
