using Arch.Core;
using Arch.SystemGroups;
using DCL.CharacterMotion.Components;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SingleScene;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateBefore(typeof(ResolveSceneStateByIncreasingRadiusSystem))]
    public partial class UpdateSingleSceneAnchorSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly SingleSceneMode singleSceneMode;
        private readonly Entity playerEntity;

        internal UpdateSingleSceneAnchorSystem(World world, SingleSceneMode singleSceneMode, Entity playerEntity) : base(world)
        {
            this.singleSceneMode = singleSceneMode;
            this.playerEntity = playerEntity;
        }

        // The anchor belongs to the realm that is going away; the next teleport sets the new one
        public void FinalizeComponents(in Query query) =>
            singleSceneMode.ClearAnchor();

        protected override void Update(float t)
        {
            if (!singleSceneMode.IsActive)
                return;

            if (World.TryGet(playerEntity, out PlayerTeleportIntent intent))
                singleSceneMode.SetAnchor(intent.DestinationParcel);
        }
    }
}
