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

            if (World.TryGet(playerEntity, out PlayerTeleportIntent intent))
                singleSceneMode.SetAnchor(intent.DestinationParcel);
        }
    }
}
