using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.Character;
using DCL.Character.Components;
using DCL.ECSComponents;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using DCL.Multiplayer.SDK.Components;
using DCL.SDKComponents.AvatarLocomotion.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using SceneRunner.Scene;
using System;

namespace DCL.SDKComponents.AvatarLocomotion.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    public partial class PropagateAvatarLocomotionOverridesSystem : BaseUnityLoopSystem
    {
        private static readonly QueryDescription GLOBAL_PLAYER_QUERY = new QueryDescription().WithAll<PlayerComponent, AvatarLocomotionOverrides>();

        private readonly ISceneStateProvider sceneStateProvider;
        private readonly World globalWorld;

        internal PropagateAvatarLocomotionOverridesSystem(World world, ISceneStateProvider sceneStateProvider, World globalWorld) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.globalWorld = globalWorld;
        }

        protected override void Update(float t)
        {
            var globalPlayerEntity = globalWorld.GetSingleInstanceEntityOrNull(GLOBAL_PLAYER_QUERY);
            if (globalPlayerEntity == Entity.Null) return;

            HandleComponentRemovedQuery(World, globalPlayerEntity);
            PropagateOverridesQuery(World, globalPlayerEntity);
        }

        [Query]
        [None(typeof(PBAvatarLocomotionSettings))]
        [All(typeof(AvatarLocomotionOverridesApplied))]
        private void HandleComponentRemoved([Data] Entity globalPlayerEntity, Entity entity)
        {
            if (!sceneStateProvider.IsCurrent) return;

            globalWorld.Set(globalPlayerEntity, AvatarLocomotionOverrides.NO_OVERRIDES);

            World.Remove<AvatarLocomotionOverridesApplied>(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void PropagateOverrides([Data] Entity globalPlayerEntity, Entity entity, in CRDTEntity crdtEntity, in PBAvatarLocomotionSettings pbSettings)
        {
            if (!sceneStateProvider.IsCurrent)
            {
                // We need to re-apply the overrides when coming back to the scene
                pbSettings.IsDirty = true;
                return;
            }

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

        private struct AvatarLocomotionOverridesApplied
        {
        }
    }
}
