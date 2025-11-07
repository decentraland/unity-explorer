using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Systems;
using DCL.SDKComponents.AvatarLocomotion.Components;
using ECS.Abstract;

namespace DCL.SDKComponents.AvatarLocomotion.Systems
{
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(SetupAvatarLocomotionOverridesSystem))]
    [UpdateBefore(typeof(CalculateCharacterVelocitySystem))]
    public partial class ApplyAvatarLocomotionOverridesSystem : BaseUnityLoopSystem
    {
        public ApplyAvatarLocomotionOverridesSystem(World world) : base(world)
        {
        }

        protected override void Update(float t) =>
            ApplyOverridesQuery(World);

        [Query]
        public void ApplyOverrides(Entity entity, ref OverridableCharacterControllerSettings settings, in AvatarLocomotionOverrides locomotionOverrides) =>
            settings.ApplyOverrides(locomotionOverrides);
    }
}
