using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components.Conversion;
using CrdtEcsBridge.ECSToCRDTWriter;
using CrdtEcsBridge.Physics;
using DCL.Character.Components;
using DCL.SDKEntityTriggerArea.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using DCL.Optimization.Pools;
using DCL.SDKComponents.TriggerArea.Components;
using DCL.SDKComponents.Utils;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using UnityEngine;
using Utility;

namespace DCL.SDKComponents.TriggerArea.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    [LogCategory(ReportCategory.CHARACTER_TRIGGER_AREA)]
    public partial class TriggerAreaHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly World globalWorld;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly IComponentPool<PBTriggerAreaResult> triggerAreaResultPool;
        private readonly IComponentPool<PBTriggerAreaResult.Types.Trigger> triggerAreaResultTriggerPool;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IEntityCollidersSceneCache collidersSceneCache;
        private readonly ISceneData sceneData;

        public TriggerAreaHandlerSystem(
            World world,
            World globalWorld,
            IECSToCRDTWriter ecsToCRDTWriter,
            IComponentPool<PBTriggerAreaResult> triggerAreaResultPool,
            IComponentPool<PBTriggerAreaResult.Types.Trigger> triggerAreaResultTriggerPool,
            ISceneStateProvider sceneStateProvider,
            IEntityCollidersSceneCache collidersSceneCache,
            ISceneData sceneData) : base(world)
        {
            this.globalWorld = globalWorld;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.triggerAreaResultPool = triggerAreaResultPool;
            this.triggerAreaResultTriggerPool = triggerAreaResultTriggerPool;
            this.sceneStateProvider = sceneStateProvider;
            this.collidersSceneCache = collidersSceneCache;
            this.sceneData = sceneData;
        }

        protected override void Update(float t)
        {
            SetupTriggerAreaQuery(World);
            UpdateTriggerAreaQuery(World);
        }

        [Query]
        [None(typeof(SDKEntityTriggerAreaComponent), typeof(SDKTriggerAreaComponent))]
        [All(typeof(TransformComponent))]
        private void SetupTriggerArea(Entity entity, in PBTriggerArea pbTriggerArea)
        {
            World.Add(
                entity,
                new SDKTriggerAreaComponent(),
                new SDKEntityTriggerAreaComponent(
                    areaSize: Vector3.zero,
                    targetOnlyMainPlayer: false,
                    meshType: (SDKEntityTriggerAreaMeshType)pbTriggerArea.GetMeshType(),
                    layerMask: pbTriggerArea.GetColliderLayer()));
        }

        [Query]
        [All(typeof(PBTriggerArea))]
        private void UpdateTriggerArea(Entity entity, in CRDTEntity triggerAreaCRDTEntity, in TransformComponent transform, ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            ProcessOnEnterTriggerArea(entity, triggerAreaCRDTEntity, transform, ref triggerAreaComponent);
            ProcessOnStayInTriggerArea(entity, triggerAreaCRDTEntity, transform, ref triggerAreaComponent);
            ProcessOnExitTriggerArea(entity, triggerAreaCRDTEntity, transform, ref triggerAreaComponent);
        }

        private void ProcessOnEnterTriggerArea(in Entity triggerAreaEntity, in CRDTEntity triggerAreaCRDTEntity, in TransformComponent transform, ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            foreach (Collider entityCollider in triggerAreaComponent.EnteredEntitiesToBeProcessed)
            {
                PropagateResultComponent(triggerAreaEntity, triggerAreaCRDTEntity, transform.Transform,
                    entityCollider, TriggerAreaEventType.TaetEnter, triggerAreaComponent.LayerMask);
            }
            triggerAreaComponent.TryClearEnteredAvatarsToBeProcessed();
        }

        private void ProcessOnStayInTriggerArea(in Entity triggerAreaEntity, in CRDTEntity triggerAreaCRDTEntity, in TransformComponent transform, ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            foreach (Collider entityCollider in triggerAreaComponent.CurrentEntitiesInside)
            {
                PropagateResultComponent(triggerAreaEntity, triggerAreaCRDTEntity, transform.Transform,
                    entityCollider, TriggerAreaEventType.TaetStay, triggerAreaComponent.LayerMask);
            }
        }

        private void ProcessOnExitTriggerArea(in Entity triggerAreaEntity, in CRDTEntity triggerAreaCRDTEntity, in TransformComponent transform, ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            foreach (Collider entityCollider in triggerAreaComponent.ExitedEntitiesToBeProcessed)
            {
                PropagateResultComponent(triggerAreaEntity, triggerAreaCRDTEntity, transform.Transform,
                    entityCollider, TriggerAreaEventType.TaetExit, triggerAreaComponent.LayerMask);
            }
            triggerAreaComponent.TryClearExitedAvatarsToBeProcessed();
        }

        private void PropagateResultComponent(in Entity triggerAreaEntity, in CRDTEntity triggerAreaCRDTEntity, Transform triggerAreaTransform,
            Collider triggerEntityCollider, TriggerAreaEventType eventType, ColliderLayer areaLayerMask)
        {
            Entity avatarEntity = Entity.Null;
            ColliderSceneEntityInfo entityInfo = default;
            if (triggerEntityCollider.gameObject.layer == PhysicsLayers.CHARACTER_LAYER
                || triggerEntityCollider.gameObject.layer == PhysicsLayers.OTHER_AVATARS_LAYER)
            {
                if (!PhysicsLayers.LayerMaskContainsTargetLayer(areaLayerMask, ColliderLayer.ClPlayer)
                    || !TryGetAvatarEntity(triggerEntityCollider.transform, out avatarEntity))
                    return;
            }

            Decentraland.Common.Vector3 triggerEntityPos = default;
            Decentraland.Common.Quaternion triggerEntityRot = default;
            Decentraland.Common.Vector3 triggerEntityScale = default;
            if (avatarEntity == Entity.Null)
            {
                if (!collidersSceneCache.TryGetEntity(triggerEntityCollider, out entityInfo)
                || triggerAreaEntity == entityInfo.EntityReference // same TriggerArea entity holding a Collider comp
                || !PhysicsLayers.LayerMaskContainsTargetLayer(areaLayerMask, entityInfo.SDKLayer)
                || !World.TryGet(entityInfo.EntityReference, out TransformComponent transformComponent))
                    return;
                triggerEntityPos = transformComponent.Transform.localPosition.ToProtoVector();
                triggerEntityRot = transformComponent.Transform.localRotation.ToProtoQuaternion();
                triggerEntityScale = transformComponent.Transform.localScale.ToProtoVector();
            }
            else
            {
                if (!globalWorld.TryGet(avatarEntity, out CharacterTransform characterTransform))
                    return;
                triggerEntityPos = characterTransform.Transform.localPosition.FromGlobalToSceneRelativePosition(sceneData.SceneShortInfo.BaseParcel).ToProtoVector();
                triggerEntityRot = characterTransform.Transform.localRotation.ToProtoQuaternion();
                triggerEntityScale = characterTransform.Transform.localScale.ToProtoVector();
            }

            var resultComponent = triggerAreaResultPool.Get();
            resultComponent.EventType = eventType;
            resultComponent.TriggeredEntity = (uint)triggerAreaCRDTEntity.Id;
            resultComponent.Timestamp = sceneStateProvider.TickNumber;
            resultComponent.TriggeredEntityPosition = triggerAreaTransform.localPosition.ToProtoVector();
            resultComponent.TriggeredEntityRotation = triggerAreaTransform.localRotation.ToProtoQuaternion();

            // 'Trigger' Entity (the entity that provokes the trigger event)
            resultComponent.Trigger = triggerAreaResultTriggerPool.Get();
            resultComponent.Trigger.Entity = avatarEntity == Entity.Null ? (uint)entityInfo.SDKEntity.Id : 99999999; // TODO: get scene CRDT Entity for player
            resultComponent.Trigger.Layer = avatarEntity == Entity.Null ? (uint)entityInfo.SDKLayer : (uint)ColliderLayer.ClPlayer;
            resultComponent.Trigger.Position = triggerEntityPos;
            resultComponent.Trigger.Rotation = triggerEntityRot;
            resultComponent.Trigger.Scale = triggerEntityScale;

            ecsToCRDTWriter.AppendMessage<PBTriggerAreaResult, (PBTriggerAreaResult result, uint timestamp)>
            (
                prepareMessage: static (pbTriggerAreaResult, data) =>
                {
                    pbTriggerAreaResult.EventType = data.result.EventType;
                    pbTriggerAreaResult.TriggeredEntity = data.result.TriggeredEntity;
                    pbTriggerAreaResult.Timestamp = data.timestamp;
                    pbTriggerAreaResult.TriggeredEntityRotation = data.result.TriggeredEntityRotation;
                    pbTriggerAreaResult.TriggeredEntityPosition = data.result.TriggeredEntityPosition;
                    pbTriggerAreaResult.Trigger = data.result.Trigger;
                },
                triggerAreaCRDTEntity, (int)sceneStateProvider.TickNumber, (resultComponent, sceneStateProvider.TickNumber)
            );

            triggerAreaResultTriggerPool.Release(resultComponent.Trigger);
            triggerAreaResultPool.Release(resultComponent);
        }

        [Query]
        [All(typeof(DeleteEntityIntention), typeof(PBTriggerArea))]
        private void HandleEntityDestruction(Entity entity, in CRDTEntity triggerAreaCRDTEntity, in TransformComponent transform, ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            ProcessOnExitTriggerArea(entity, triggerAreaCRDTEntity, transform, ref triggerAreaComponent);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBTriggerArea))]
        private void HandleComponentRemoval(Entity entity, in CRDTEntity triggerAreaCRDTEntity, in TransformComponent transform, ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            ProcessOnExitTriggerArea(entity, triggerAreaCRDTEntity, transform, ref triggerAreaComponent);
            World.Remove<SDKTriggerAreaComponent>(entity);
        }

        [Query]
        private void FinalizeComponents(Entity entity, in CRDTEntity triggerAreaCRDTEntity, in TransformComponent transform, ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            ProcessOnExitTriggerArea(entity, triggerAreaCRDTEntity, transform, ref triggerAreaComponent);
            World.Remove<SDKTriggerAreaComponent>(entity);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }

        private bool TryGetAvatarEntity(Transform transform, out Entity entity)
        {
            entity = Entity.Null;
            var result = FindAvatarUtils.AvatarWithTransform(globalWorld, transform);
            if (!result.Success) return false;
            entity = result.Result;
            return true;
        }
    }
}



