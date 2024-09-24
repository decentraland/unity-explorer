using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
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
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PlayerCRDTEntitiesHandlerSystem))]
    [LogCategory(ReportCategory.PLAYER_SDK_DATA)]
    public partial class PlayerProfileDataPropagationSystem : BaseUnityLoopSystem
    {
        private readonly ICharacterDataPropagationUtility characterDataPropagationUtility;

        public PlayerProfileDataPropagationSystem(World world, ICharacterDataPropagationUtility characterDataPropagationUtility) : base(world)
        {
            this.characterDataPropagationUtility = characterDataPropagationUtility;
        }

        protected override void Update(float t)
        {
            PropagateProfileToSceneQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void PropagateProfileToScene(Profile profile, in PlayerCRDTEntity playerCRDTEntity)
        {
            if ((playerCRDTEntity.IsDirty || profile.IsDirty) && playerCRDTEntity.AssignedToScene)
                characterDataPropagationUtility.CopyProfileToSceneEntity(profile, playerCRDTEntity.SceneFacade!.EcsExecutor, playerCRDTEntity.SceneWorldEntity);
        }
    }
}
