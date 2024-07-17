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
            PropagateTransformToSceneQuery(World!);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void PropagateTransformToScene(ref IAvatarView avatarView, ref PlayerCRDTEntity playerCRDTEntity)
        {
            // Main player Transform is handled by 'WriteMainPlayerTransformSystem'
            if (playerCRDTEntity.CRDTEntity.Id == SpecialEntitiesID.PLAYER_ENTITY) return;
            PropagateComponent(ref avatarView, ref playerCRDTEntity, playerCRDTEntity.IsDirty == false);
        }

        private static void PropagateComponent(ref IAvatarView avatarBase, ref PlayerCRDTEntity playerCRDTEntity, bool useSet = false)
        {
            SceneEcsExecutor sceneEcsExecutor = playerCRDTEntity.SceneFacade.EcsExecutor;

            var sdkTransform = new SDKTransform
            {
                Position = avatarBase.Position, // updated to scene-relative on the writer system
                Rotation = avatarBase.Rotation,
            };

            if (useSet)
                sceneEcsExecutor.World.Set(playerCRDTEntity.SceneWorldEntity, sdkTransform);
            else
                sceneEcsExecutor.World.Add(playerCRDTEntity.SceneWorldEntity, sdkTransform);
        }
    }
}
