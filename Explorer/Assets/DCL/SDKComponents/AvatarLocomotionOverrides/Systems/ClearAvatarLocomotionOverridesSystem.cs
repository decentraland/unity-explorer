using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterMotion.Systems;
using DCL.SDKComponents.AvatarLocomotion.Components;
using ECS.Abstract;
using ECS.SceneLifeCycle;

namespace DCL.SDKComponents.AvatarLocomotion.Systems
{
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(CalculateCharacterVelocitySystem))]
    public partial class ClearAvatarLocomotionOverridesSystem : BaseUnityLoopSystem
    {
        private readonly IScenesCache scenesCache;

        public ClearAvatarLocomotionOverridesSystem(World world, IScenesCache scenesCache) : base(world)
        {
            this.scenesCache = scenesCache;
        }

        protected override void Update(float t)
        {
            if (scenesCache.CurrentScene != null) return;

            // Clear overrides if there is no current scene
            // Otherwise it's the scene itself clearing the overrides
            // (because of throttling and different update frequencies between global and world plugins)
            ClearOverridesQuery(World);
        }

        [Query]
        public void ClearOverrides(Entity entity, ref AvatarLocomotionOverrides locomotionOverrides) =>
            AvatarLocomotionOverridesHelper.ClearAll(ref locomotionOverrides);
    }
}
