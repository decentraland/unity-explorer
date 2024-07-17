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
    [UpdateAfter(typeof(PlayerCRDTEntitiesHandlerSystem))]
    [UpdateBefore(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.MULTIPLAYER_SDK_PLAYER_PROFILE_DATA)]
    public partial class PlayerProfileDataPropagationSystem : BaseUnityLoopSystem
    {
        private readonly ProfileBuilder profileBuilder = new ();

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

            PropagateComponent(ref profile, ref playerCRDTEntity);
        }

        private void PropagateComponent(ref Profile profile, ref PlayerCRDTEntity playerCRDTEntity)
        {
            SceneEcsExecutor sceneEcsExecutor = playerCRDTEntity.SceneFacade.EcsExecutor;

            ref Profile profileComponent = ref sceneEcsExecutor.World.AddOrGet<Profile>(playerCRDTEntity.SceneWorldEntity);
            profileComponent = CloneProfile(profile);

            return;

            Profile CloneProfile(Profile p) =>
                profileBuilder.From(p)
                              .WithAvatar(p.Avatar)
                              .Build();
        }
    }
}
