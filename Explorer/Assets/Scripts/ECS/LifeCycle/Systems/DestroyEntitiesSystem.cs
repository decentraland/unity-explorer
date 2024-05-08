using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;

namespace ECS.LifeCycle.Systems
{
    /// <summary>
    ///     Destroys all entities marked for deletion
    /// </summary>
    [UpdateInGroup(typeof(SyncedPostRenderingSystemGroup))]
    [UpdateAfter(typeof(CleanUpGroup))]
    [ThrottlingEnabled]
    public partial class DestroyEntitiesSystem : BaseUnityLoopSystem
    {
        internal DestroyEntitiesSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            DeleteEntitiesQuery(World);
        }

        [Query]
        private void DeleteEntities(in Entity entity, ref DeleteEntityIntention deleteEntityIntention)
        {
            if (!deleteEntityIntention.DeferDeletion)
                World.Destroy(entity);
        }
    }
}
