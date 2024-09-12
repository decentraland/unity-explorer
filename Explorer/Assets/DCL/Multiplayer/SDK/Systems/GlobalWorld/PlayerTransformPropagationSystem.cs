using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Transform;
using DCL.Diagnostics;
using DCL.Multiplayer.SDK.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;

namespace DCL.Multiplayer.SDK.Systems.GlobalWorld
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [LogCategory(ReportCategory.PLAYER_SDK_DATA)]
    public partial class PlayerTransformPropagationSystem : BaseUnityLoopSystem
    {
        public PlayerTransformPropagationSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            PropagateTransformToSceneQuery(World!);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void PropagateTransformToScene(in TransformComponent transformComponent, in PlayerCRDTEntity playerCRDTEntity)
        {
            // Main player Transform is handled by 'WriteMainPlayerTransformSystem'
            if (playerCRDTEntity.CRDTEntity.Id == SpecialEntitiesID.PLAYER_ENTITY) return;

            World sceneEcsWorld = playerCRDTEntity.SceneFacade.EcsExecutor.World;

            var sdkTransform = new SDKTransform
            {
                Position = transformComponent.Transform.position, // updated to scene-relative on the writer system
                Rotation = transformComponent.Transform.rotation,
            };

            if (sceneEcsWorld.Has<SDKTransform>(playerCRDTEntity.SceneWorldEntity))
                sceneEcsWorld.Set(playerCRDTEntity.SceneWorldEntity, sdkTransform);
            else
                sceneEcsWorld.Add(playerCRDTEntity.SceneWorldEntity, sdkTransform);
        }
    }
}
