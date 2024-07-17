using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Transform;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using DCL.Multiplayer.SDK.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;

namespace DCL.Multiplayer.SDK.Systems.GlobalWorld
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [LogCategory(ReportCategory.MULTIPLAYER_SDK_PLAYER_TRANSFORM_DATA)]
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

            PropagateComponent(ref avatarView, ref playerCRDTEntity);
        }

        private static void PropagateComponent(ref IAvatarView avatarBase, ref PlayerCRDTEntity playerCRDTEntity)
        {
            World sceneEcsWorld = playerCRDTEntity.SceneFacade.EcsExecutor.World;

            var sdkTransform = new SDKTransform
            {
                Position = avatarBase.Position, // updated to scene-relative on the writer system
                Rotation = avatarBase.Rotation,
            };

            if (sceneEcsWorld.Has<SDKTransform>(playerCRDTEntity.SceneWorldEntity))
                sceneEcsWorld.Set(playerCRDTEntity.SceneWorldEntity, sdkTransform);
            else
                sceneEcsWorld.Add(playerCRDTEntity.SceneWorldEntity, sdkTransform);
        }
    }
}
