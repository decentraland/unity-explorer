using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Transform;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Multiplayer.SDK.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using SceneRunner.Scene;

namespace DCL.Multiplayer.SDK.Systems.GlobalWorld
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    // [UpdateBefore(typeof(CleanUpGroup))]
    // [LogCategory(ReportCategory.MULTIPLAYER_SDK_PLAYER_PROFILE_DATA)]
    public partial class PlayerTransformPropagationSystem : BaseUnityLoopSystem
    {
        public PlayerTransformPropagationSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            PropagateTransformToSceneQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void PropagateTransformToScene(ref AvatarBase avatarBase, ref PlayerCRDTEntity playerCRDTEntity)
        {
            // Main player Transform is handled by 'WriteMainPlayerTransformSystem'
            if (playerCRDTEntity.CRDTEntity.Id == SpecialEntitiesID.PLAYER_ENTITY) return;

            if (playerCRDTEntity.IsDirty)
            {
                PropagateComponent(ref avatarBase, ref playerCRDTEntity);
                return;
            }

            PropagateComponent(ref avatarBase, ref playerCRDTEntity, true);
        }

        private void PropagateComponent(ref AvatarBase avatarBase, ref PlayerCRDTEntity playerCRDTEntity, bool useSet = false)
        {
            SceneEcsExecutor sceneEcsExecutor = playerCRDTEntity.SceneFacade.EcsExecutor;

            var avatarTransform = avatarBase.transform;
            var sdkTransform = new SDKTransform()
            {
                Position = avatarTransform.position, // updated to scene-relative on the writer system
                Rotation = avatarTransform.transform.rotation
            };

            if (useSet)
                sceneEcsExecutor.World.Set(playerCRDTEntity.SceneWorldEntity, sdkTransform);
            else
                sceneEcsExecutor.World.Add(playerCRDTEntity.SceneWorldEntity, sdkTransform);
        }
    }
}
