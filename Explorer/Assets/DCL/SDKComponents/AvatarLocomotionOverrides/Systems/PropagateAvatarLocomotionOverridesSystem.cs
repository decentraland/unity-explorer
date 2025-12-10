using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.Character;
using DCL.Character.Components;
using DCL.ECSComponents;
using DCL.SDKComponents.AvatarLocomotion.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using SceneRunner.Scene;

namespace DCL.SDKComponents.AvatarLocomotion.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    public partial class PropagateAvatarLocomotionOverridesSystem : BaseUnityLoopSystem, ISceneIsCurrentListener
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly World globalWorld;
        private Entity globalPlayerEntity;

        internal PropagateAvatarLocomotionOverridesSystem(World world, ISceneStateProvider sceneStateProvider, World globalWorld) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.globalWorld = globalWorld;
        }

        public override void Initialize() =>
            globalPlayerEntity = globalWorld.CachePlayer();

        protected override void Update(float t)
        {
            if (!sceneStateProvider.IsCurrent) return;

            HandleComponentRemovedQuery(World);
            PropagateOverridesQuery(World);
        }

        [Query]
        [None(typeof(PBAvatarLocomotionSettings))]
        [All(typeof(AvatarLocomotionOverridesApplied))]
        private void HandleComponentRemoved(Entity entity)
        {
            globalWorld.Set(globalPlayerEntity, AvatarLocomotionOverrides.NO_OVERRIDES);

            World.Remove<AvatarLocomotionOverridesApplied>(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void PropagateOverrides(Entity entity, in CRDTEntity crdtEntity, in PBAvatarLocomotionSettings pbSettings)
        {
            if (!pbSettings.IsDirty) return;

            // Only apply settings if the component was added to the local player entity
            if (crdtEntity.Id != SpecialEntitiesID.PLAYER_ENTITY) return;

            GetLocomotionOverrides(pbSettings, out AvatarLocomotionOverrides locomotionOverrides);
            globalWorld.Set(globalPlayerEntity, locomotionOverrides);

            pbSettings.IsDirty = false;
            World.Add<AvatarLocomotionOverridesApplied>(entity);
        }

        private static void GetLocomotionOverrides(PBAvatarLocomotionSettings pbSettings, out AvatarLocomotionOverrides locomotionOverrides)
        {
            locomotionOverrides = new AvatarLocomotionOverrides();

            if (pbSettings.HasWalkSpeed) AvatarLocomotionOverridesHelper.SetValue(ref locomotionOverrides, AvatarLocomotionOverrides.OverrideID.WALK_SPEED, pbSettings.WalkSpeed);
            if (pbSettings.HasJogSpeed) AvatarLocomotionOverridesHelper.SetValue(ref locomotionOverrides, AvatarLocomotionOverrides.OverrideID.JOG_SPEED, pbSettings.JogSpeed);
            if (pbSettings.HasRunSpeed) AvatarLocomotionOverridesHelper.SetValue(ref locomotionOverrides, AvatarLocomotionOverrides.OverrideID.RUN_SPEED, pbSettings.RunSpeed);
            if (pbSettings.HasJumpHeight) AvatarLocomotionOverridesHelper.SetValue(ref locomotionOverrides, AvatarLocomotionOverrides.OverrideID.JUMP_HEIGHT, pbSettings.JumpHeight);
            if (pbSettings.HasRunJumpHeight) AvatarLocomotionOverridesHelper.SetValue(ref locomotionOverrides, AvatarLocomotionOverrides.OverrideID.RUN_JUMP_HEIGHT, pbSettings.RunJumpHeight);
            if (pbSettings.HasHardLandingCooldown) AvatarLocomotionOverridesHelper.SetValue(ref locomotionOverrides, AvatarLocomotionOverrides.OverrideID.HARD_LANDING_COOLDOWN, pbSettings.HardLandingCooldown);
        }

        public void OnSceneIsCurrentChanged(bool isCurrent)
        {
            if (isCurrent) return;

            globalWorld.Set(globalPlayerEntity, AvatarLocomotionOverrides.NO_OVERRIDES);

            // We need to re-apply the overrides when coming back to the scene
            MarkSettingsDirtyQuery(World);
        }

        [Query]
        private void MarkSettingsDirty(ref PBAvatarLocomotionSettings pbSettings) =>
            pbSettings.IsDirty = true;

        private struct AvatarLocomotionOverridesApplied
        {
        }
    }
}
