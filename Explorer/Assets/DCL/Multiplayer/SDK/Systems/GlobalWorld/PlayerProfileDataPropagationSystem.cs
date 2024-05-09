using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Multiplayer.SDK.Components;
using DCL.Profiles;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.LifeCycle.Systems;
using SceneRunner.Scene;

namespace DCL.Multiplayer.SDK.Systems.GlobalWorld
{
    [UpdateBefore(typeof(ResetDirtyFlagSystem<Profile>))]
    [UpdateInGroup(typeof(SyncedPostRenderingSystemGroup))]
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
                PropagateComponent(ref profile, ref playerCRDTEntity);
                return;
            }

            if (!profile.IsDirty) return;

            PropagateComponent(ref profile, ref playerCRDTEntity, true);
        }

        private void PropagateComponent(ref Profile profile, ref PlayerCRDTEntity playerCRDTEntity, bool useSet = false)
        {
            SceneEcsExecutor sceneEcsExecutor = playerCRDTEntity.SceneFacade.EcsExecutor;

            // External world access should be always synchronized (Global World calls into Scene World)
            using (sceneEcsExecutor.Sync.GetScope())
            {
                if (useSet)
                    sceneEcsExecutor.World.Set(playerCRDTEntity.SceneWorldEntity, new Profile(profile.UserId, profile.Name, profile.Avatar));
                else
                    sceneEcsExecutor.World.Add(playerCRDTEntity.SceneWorldEntity, new Profile(profile.UserId, profile.Name, profile.Avatar));
            }
        }
    }
}
