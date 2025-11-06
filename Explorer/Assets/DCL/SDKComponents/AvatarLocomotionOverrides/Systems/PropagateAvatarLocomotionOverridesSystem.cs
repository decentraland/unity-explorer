using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using DCL.Character;
using DCL.Character.Components;
using DCL.ECSComponents;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using DCL.Multiplayer.SDK.Components;
using DCL.SDKComponents.AvatarLocomotion.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using SceneRunner.Scene;
using System;

namespace DCL.SDKComponents.AvatarLocomotion.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    public partial class PropagateAvatarLocomotionOverridesSystem : BaseUnityLoopSystem
    {
        private static readonly QueryDescription GLOBAL_PLAYER_QUERY = new QueryDescription().WithAll<PlayerComponent, AvatarLocomotionOverrides>();
        private static readonly QueryDescription SCENE_PLAYER_QUERY = new QueryDescription().WithAll<PlayerSceneCRDTEntity>();

        private readonly ISceneStateProvider sceneStateProvider;
        private readonly World globalWorld;

        internal PropagateAvatarLocomotionOverridesSystem(World world, ISceneStateProvider sceneStateProvider, World globalWorld) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.globalWorld = globalWorld;
        }

        protected override void Update(float t)
        {
            if (!sceneStateProvider.IsCurrent) return;

            var globalPlayerEntity = globalWorld.GetSingleInstanceEntityOrNull(GLOBAL_PLAYER_QUERY);
            if (globalPlayerEntity == Entity.Null) return;

            // Overrides are always cleared but only propagated if there is an actual SDK component for it
            // Clearing cannot be done in the global world because of the difference in update frequencies between global and world plugins
            ClearOverrides(globalPlayerEntity);
            PropagateOverridesQuery(World, globalPlayerEntity);
        }

        private void ClearOverrides(Entity globalPlayerEntity) =>
            globalWorld.Set(globalPlayerEntity, AvatarLocomotionOverrides.NO_OVERRIDES);

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void PropagateOverrides([Data] Entity globalPlayerEntity, in CRDTEntity crdtEntity, in PBAvatarLocomotionSettings pbSettings)
        {
            // Only apply settings if the component was added to the player entity
            if (!TryGetPlayerCrdtId(out int playerCrdtId) || playerCrdtId != crdtEntity.Id) return;

            GetLocomotionOverrides(pbSettings, out AvatarLocomotionOverrides locomotionOverrides);
            globalWorld.Set(globalPlayerEntity, locomotionOverrides);
        }

        private bool TryGetPlayerCrdtId(out int crdtId)
        {
            var playerCrdtEntity = World.GetSingleInstanceEntityOrNull(SCENE_PLAYER_QUERY);

            if (playerCrdtEntity != Entity.Null && World.TryGet<PlayerSceneCRDTEntity>(playerCrdtEntity, out var playerCrdt))
            {
                crdtId = playerCrdt.CRDTEntity.Id;
                return true;
            }

            crdtId = -1;
            return false;
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
    }
}
