using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
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
    [UpdateBefore(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.MULTIPLAYER_SDK_PLAYER_PROFILE_DATA)]
    public partial class PlayerProfileDataPropagationSystem : BaseUnityLoopSystem
    {
        public PlayerProfileDataPropagationSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            PropagateProfileToSceneQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void PropagateProfileToScene(ref Profile profile, ref PlayerCRDTEntity playerCRDTEntity)
        {
            if (playerCRDTEntity.IsDirty)
            {
                playerCRDTEntity.IsDirty = false;
                SetSceneProfile(ref profile, ref playerCRDTEntity);
                return;
            }

            if (!profile.IsDirty) return;

            SetSceneProfile(ref profile, ref playerCRDTEntity);
        }

        private void SetSceneProfile(ref Profile profile, ref PlayerCRDTEntity playerCRDTEntity)
        {
            SceneEcsExecutor sceneEcsExecutor = playerCRDTEntity.SceneFacade.EcsExecutor;
            sceneEcsExecutor.World.Add(playerCRDTEntity.SceneWorldEntity, new Profile(profile.UserId, profile.Name, profile.Avatar));
        }
    }
}
