using Arch.Core;
using Arch.SystemGroups;
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
    public partial class DestroyEntitiesSystem : BaseUnityLoopSystem
    {
        private readonly QueryDescription query = new QueryDescription().WithAll<DeleteEntityIntention>();

        internal DestroyEntitiesSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            World.Destroy(in query);
        }
    }
}
