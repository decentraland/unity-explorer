using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using DCL.Character;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.SDKComponents.AvatarLocomotion.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using NBitcoin;
using SceneRunner.Scene;
using Utility.Arch;

namespace DCL.SDKComponents.AvatarLocomotion.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    public partial class PropagateAvatarLocomotionSettingsSystem : BaseUnityLoopSystem
    {
        private static readonly QueryDescription SCENE_PLAYER_QUERY = new QueryDescription().WithAll<PlayerSceneCRDTEntity>();

        private readonly ISceneStateProvider sceneStateProvider;
        private readonly World globalWorld;

        internal PropagateAvatarLocomotionSettingsSystem(
            World world,
            ISceneStateProvider sceneStateProvider,
            World globalWorld) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.globalWorld = globalWorld;
        }

        protected override void Update(float t) =>
            PropagateSettingsQuery(World);

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void PropagateSettings(in CRDTEntity crdtEntity, in PBAvatarLocomotionSettings pbSettings)
        {
            if (!sceneStateProvider.IsCurrent) return;

            // Only apply settings if the component was added to the player entity
            if (!TryGetPlayerCrdtId(out int playerCrdtId) || playerCrdtId != crdtEntity.Id) return;

            var globalPlayer = globalWorld.CachePlayer();
            if (globalPlayer == Entity.Null) return;

            globalWorld.Set(globalPlayer,
                new AvatarLocomotionSettings
                {
                    JogSpeed = pbSettings.JogSpeed,
                    WalkSpeed = pbSettings.WalkSpeed,
                    RunSpeed = pbSettings.RunSpeed,
                    JumpHeight = pbSettings.JumpHeight,
                    RunJumpHeight = pbSettings.RunJumpHeight
                });
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

    }
}
