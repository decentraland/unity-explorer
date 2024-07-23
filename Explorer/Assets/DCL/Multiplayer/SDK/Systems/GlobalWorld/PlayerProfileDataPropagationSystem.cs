using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.SDK.Components;
using DCL.Profiles;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using SceneRunner.Scene;

namespace DCL.Multiplayer.SDK.Systems.GlobalWorld
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [UpdateAfter(typeof(PlayerCRDTEntitiesHandlerSystem))]
    [UpdateBefore(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.PLAYER_SDK_DATA)]
    public partial class PlayerProfileDataPropagationSystem : BaseUnityLoopSystem
    {
        private readonly ICharacterDataPropagationUtility characterDataPropagationUtility;
        private readonly Entity playerEntity;

        public PlayerProfileDataPropagationSystem(World world, ICharacterDataPropagationUtility characterDataPropagationUtility, Entity playerEntity) : base(world)
        {
            this.characterDataPropagationUtility = characterDataPropagationUtility;
            this.playerEntity = playerEntity;
        }

        protected override void Update(float t)
        {
            // Our player must be propagated to all scenes as their logic can rely on it
            if (World.TryGet(playerEntity, out Profile? profile) && profile!.IsDirty)
                PropagatePlayerProfileToAliveScenesQuery(World, profile);

            PropagateProfileToSceneQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PlayerComponent))]
        private void PropagateProfileToScene(Profile profile, PlayerCRDTEntity playerCRDTEntity)
        {
            if (playerCRDTEntity.IsDirty || profile.IsDirty)
                characterDataPropagationUtility.CopyProfileToSceneEntity(profile, playerCRDTEntity.SceneFacade.EcsExecutor, playerCRDTEntity.SceneWorldEntity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void PropagatePlayerProfileToAliveScenes([Data] Profile playerProfile, ISceneFacade sceneFacade)
        {
            if (sceneFacade.IsEmpty) return;

            characterDataPropagationUtility.CopyProfileToSceneEntity(playerProfile, sceneFacade.EcsExecutor, sceneFacade.PersistentEntities.Player);
        }
    }
}
