using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Systems;
using DCL.SDKComponents.AvatarLocomotion.Components;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using Unity.Mathematics;

namespace DCL.SDKComponents.AvatarLocomotion.Systems
{
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(SetupAvatarLocomotionOverridesSystem))]
    [UpdateBefore(typeof(CalculateCharacterVelocitySystem))]
    public partial class ApplyAvatarLocomotionOverridesSystem : BaseUnityLoopSystem
    {
        private readonly AvatarLocomotionOverridesGlobalPlugin.Settings settings;
        private readonly IScenesCache scenesCache;

        public ApplyAvatarLocomotionOverridesSystem(World world, AvatarLocomotionOverridesGlobalPlugin.Settings settings, IScenesCache scenesCache) : base(world)
        {
            this.settings = settings;
            this.scenesCache = scenesCache;
        }

        protected override void Update(float t)
        {
            // If there is no current scene, we need to clear the overrides
            // Normally it's the current scene that does it (see PropagateAvatarLocomotionOverridesSystem)
            if (scenesCache.CurrentScene.Value == null) ClearOverridesQuery(World);

            ApplyOverridesQuery(World);
        }

        [Query]
        [All(typeof(AvatarLocomotionOverrides))]
        public void ClearOverrides(Entity entity) =>
            World.Set(entity, AvatarLocomotionOverrides.NO_OVERRIDES);

        [Query]
        public void ApplyOverrides(Entity entity, ref OverridableCharacterControllerSettings settings, in AvatarLocomotionOverrides locomotionOverrides)
        {
            var clampedOverrides = ClampOverrides(locomotionOverrides);
            settings.ApplyOverrides(clampedOverrides);
        }

        private AvatarLocomotionOverrides ClampOverrides(in AvatarLocomotionOverrides locomotionOverrides)
        {
            var clampedOverrides = locomotionOverrides;

            clampedOverrides.WalkSpeed = math.clamp(locomotionOverrides.WalkSpeed, 0, settings.MaxMovementSpeed);
            clampedOverrides.JogSpeed = math.clamp(locomotionOverrides.JogSpeed, 0, settings.MaxMovementSpeed);
            clampedOverrides.RunSpeed = math.clamp(locomotionOverrides.RunSpeed, 0, settings.MaxMovementSpeed);
            clampedOverrides.JumpHeight = math.clamp(locomotionOverrides.JumpHeight, 0, settings.MaxJumpHeight);
            clampedOverrides.RunJumpHeight = math.clamp(locomotionOverrides.RunJumpHeight, 0, settings.MaxJumpHeight);

            return clampedOverrides;
        }
    }
}
