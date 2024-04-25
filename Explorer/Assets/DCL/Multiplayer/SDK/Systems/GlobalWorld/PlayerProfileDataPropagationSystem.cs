using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Multiplayer.SDK.Components;
using DCL.Profiles;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.Multiplayer.SDK.Systems.GlobalWorld
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PlayerCRDTEntitiesHandlerSystem))]
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
        private void PropagateProfileToScene(Profile profile, ref PlayerCRDTEntity playerCRDTEntity)
        {
            SceneEcsExecutor sceneEcsExecutor = playerCRDTEntity.SceneFacade.EcsExecutor;

            if (playerCRDTEntity.IsDirty)
            {
                profile.IsDirty = true;

                // External world access should be always synchronized (Global World calls into Scene World)
                using (sceneEcsExecutor.Sync.GetScope())
                    sceneEcsExecutor.World.Add(playerCRDTEntity.SceneWorldEntity, profile);

                return;
            }

            if (!profile.IsDirty) return;

            // External world access should be always synchronized (Global World calls into Scene World)
            using (sceneEcsExecutor.Sync.GetScope())
                sceneEcsExecutor.World.Set(playerCRDTEntity.SceneWorldEntity, profile);
        }
    }
}
