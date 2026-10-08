using Arch.Core;
using Arch.SystemGroups;
using DCL.CharacterMotion.Components;
using ECS.Abstract;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SingleScene;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateBefore(typeof(ResolveSceneStateByIncreasingRadiusSystem))]
    public partial class UpdateSingleSceneAnchorSystem : BaseUnityLoopSystem
    {
        private readonly SingleSceneMode singleSceneMode;
        private readonly Entity playerEntity;

        internal UpdateSingleSceneAnchorSystem(World world, SingleSceneMode singleSceneMode, Entity playerEntity) : base(world)
        {
            this.singleSceneMode = singleSceneMode;
            this.playerEntity = playerEntity;
        }

        protected override void Update(float t)
        {
            if (!singleSceneMode.IsActive)
                return;

            // Every entry into a realm teleports, so an absent intent means the destination is not settled yet.
            // Leaving the anchor unset keeps the restriction off rather than pinning it to a stale position.
            if (World.TryGet(playerEntity, out PlayerTeleportIntent intent))
                singleSceneMode.SetAnchor(intent.DestinationParcel);
        }
    }
}
