using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.DeferredLoading;
using System;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(AvatarGroup))]
    public partial class AvatarQualityReductionSystem : BaseUnityLoopSystem
    {
        public AvatarQualityReductionSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            TryReduceQualityQuery(World);
        }

        [Query]
        private void TryReduceQuality(in Entity entity, AvatarBase avatarBase, ref QualityReductionRequest reductionRequest)
        {
            World.Add(entity, new DeleteEntityIntention());
        }
    }
}
