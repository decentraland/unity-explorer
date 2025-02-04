using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;

namespace ECS.StreamableLoading.DeferredLoading
{
    public class QualityReductorManager 
    {
        private readonly World World;

        
        private bool avatarQualityReduced;

        public QualityReductorManager(World world)
        {
            World = world;
        }

        public void RequestQualityReduction()
        {
            if (!avatarQualityReduced)
            {
                World.Create(new AvatarQualityReductionRequest(true));
                avatarQualityReduced = true;
            }
        }

        public void RequestQualityIncrease()
        {
            if (avatarQualityReduced)
            {
                World.Create(new AvatarQualityReductionRequest(false));
                avatarQualityReduced = false;
            }
        }
        
    }
}
