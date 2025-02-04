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
    public partial class AvatarQualityReductorSystem : BaseUnityLoopSystem
    {
        private static readonly QueryDescription ALL_AVATARS_QUERY = new QueryDescription()
            .WithAll<AvatarBase, AvatarShapeComponent>()
            .WithNone<PlayerComponent>();

        public AvatarQualityReductorSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            TryReduceQualityQuery(World);
        }

        [Query]
        private void TryReduceQuality(in Entity entity, ref AvatarQualityReductionRequest reductionRequest)
        {
            World.Query(ALL_AVATARS_QUERY, entity => World.Add(entity, new DeleteEntityIntention()));
            World.Destroy(entity);
        }
    }
}
