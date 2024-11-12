using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Transform;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.SDK.Components;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using Utility;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace DCL.Multiplayer.SDK.Systems.GlobalWorld
{
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    [LogCategory(ReportCategory.PLAYER_SDK_DATA)]
    public partial class PlayerTransformPropagationSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<SDKTransform> sdkTransformPool;

        public PlayerTransformPropagationSystem(World world, IComponentPool<SDKTransform> sdkTransformPool) : base(world)
        {
            this.sdkTransformPool = sdkTransformPool;
        }

        protected override void Update(float t)
        {
            PropagateTransformToSceneQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void PropagateTransformToScene(in CharacterTransform characterTransform, in PlayerCRDTEntity playerCRDTEntity)
        {
            if (!characterTransform.Transform.hasChanged) return;

            if (!playerCRDTEntity.AssignedToScene) return;

            // Main player Transform is handled by 'WriteMainPlayerTransformSystem'
            if (playerCRDTEntity.CRDTEntity.Id == SpecialEntitiesID.PLAYER_ENTITY) return;

            World sceneEcsWorld = playerCRDTEntity.SceneFacade!.EcsExecutor.World;

            // Position is updated to scene-relative on the writer system
            if (!sceneEcsWorld.TryGet<SDKTransform>(playerCRDTEntity.SceneWorldEntity, out SDKTransform? sdkTransform))
                sceneEcsWorld.Add(playerCRDTEntity.SceneWorldEntity, sdkTransform = sdkTransformPool.Get());

            sdkTransform!.Position.Value = characterTransform.Transform.position;
            sdkTransform.Rotation.Value = characterTransform.Transform.rotation;
            sdkTransform.IsDirty = true;
        }
    }
}
