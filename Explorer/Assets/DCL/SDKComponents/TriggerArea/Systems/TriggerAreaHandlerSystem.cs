using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components.Conversion;
using CrdtEcsBridge.ECSToCRDTWriter;
using CrdtEcsBridge.Physics;
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
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.TriggerArea.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    [LogCategory(ReportCategory.CHARACTER_TRIGGER_AREA)]
    public partial class TriggerAreaHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly World globalWorld;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap;
        private readonly IComponentPool<PBTriggerAreaResult> triggerAreaResultPool;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IEntityCollidersSceneCache collidersSceneCache;

        public TriggerAreaHandlerSystem(
            World world,
            World globalWorld,
            IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap,
            IECSToCRDTWriter ecsToCRDTWriter,
            IComponentPool<PBTriggerAreaResult> triggerAreaResultPool,
            ISceneStateProvider sceneStateProvider,
            IEntityCollidersSceneCache collidersSceneCache) : base(world)
        {
            this.globalWorld = globalWorld;
            this.entitiesMap = entitiesMap;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.triggerAreaResultPool = triggerAreaResultPool;
            this.sceneStateProvider = sceneStateProvider;
            this.collidersSceneCache = collidersSceneCache;
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
                    meshType: pbTriggerArea.HasMesh ? (SDKEntityTriggerAreaMeshType)pbTriggerArea.Mesh : SDKEntityTriggerAreaMeshType.BOX,
                    layerMask: pbTriggerArea.HasCollisionMask ? pbTriggerArea.CollisionMask : (uint)ColliderLayer.ClPlayer)
                );
        }

        [Query]
        [All(typeof(PBTriggerArea))]
        private void UpdateTriggerArea(in CRDTEntity triggerAreaCRDTEntity, in TransformComponent transform, ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            foreach (Transform entityTransform in triggerAreaComponent.EnteredAvatarsToBeProcessed)
            {
                PropagateResultComponent(triggerAreaCRDTEntity, transform.Transform,
                    entityTransform, TriggerAreaEventType.TaetEnter, triggerAreaComponent.LayerMask);
            }
            triggerAreaComponent.TryClearEnteredAvatarsToBeProcessed();

            // TODO: STAY...
            // TODO: Can we infer the "STAY" state when ENTER was received but not EXIT ???

            foreach (Transform entityTransform in triggerAreaComponent.ExitedAvatarsToBeProcessed)
            {
                PropagateResultComponent(triggerAreaCRDTEntity, transform.Transform,
                    entityTransform, TriggerAreaEventType.TaetExit, triggerAreaComponent.LayerMask);
            }
            triggerAreaComponent.TryClearExitedAvatarsToBeProcessed();
        }

        private void PropagateResultComponent(in CRDTEntity triggerAreaCRDTEntity, Transform triggerAreaTransform,
            Transform triggerEntityTransform, TriggerAreaEventType eventType, uint areaLayerMask)
        {
            Entity avatarEntity = Entity.Null;
            ColliderSceneEntityInfo entityInfo = default;
            if (triggerEntityTransform.gameObject.layer == PhysicsLayers.CHARACTER_LAYER
                || triggerEntityTransform.gameObject.layer == PhysicsLayers.OTHER_AVATARS_LAYER)
            {
                if (!PhysicsLayers.LayerMaskContainsTargetLayer(areaLayerMask, ColliderLayer.ClPlayer)
                    || !TryGetAvatarEntity(triggerEntityTransform, out avatarEntity))
                    return;
            }

            // TODO: Improve to avoid GetComponent...
            if (avatarEntity == Entity.Null &&
                !collidersSceneCache.TryGetEntity(triggerEntityTransform.GetComponent<Collider>(), out entityInfo))
                return;

            if (!PhysicsLayers.LayerMaskContainsTargetLayer(areaLayerMask, entityInfo.SDKLayer))
                return;

            var resultComponent = triggerAreaResultPool.Get();
            resultComponent.EventType = eventType;
            resultComponent.TriggeredEntity = (uint)triggerAreaCRDTEntity.Id;
            resultComponent.Timestamp = sceneStateProvider.TickNumber;
            resultComponent.TriggeredEntityPosition = triggerAreaTransform.localPosition.ToProtoVector();
            resultComponent.TriggeredEntityRotation = triggerAreaTransform.localRotation.ToProtoQuaternion();

            // TODO: Pool this one as well ???
            // TODO: Get Players CRDT Entities to to get their relative position, etc...
            resultComponent.Trigger = new PBTriggerAreaResult.Types.Trigger()
            {
                Entity = avatarEntity == Entity.Null ? (uint)entityInfo.SDKEntity.Id : 99999999, // TODO: get scene CRDT Entity for player
                Layer = avatarEntity == Entity.Null ? (uint)entityInfo.SDKLayer : (uint)ColliderLayer.ClPlayer,
                Position = triggerEntityTransform.localPosition.ToProtoVector(),
                Rotation = triggerEntityTransform.localRotation.ToProtoQuaternion(),
                Scale = triggerEntityTransform.localScale.ToProtoVector(),
            };

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
        }

        [Query]
        [All(typeof(DeleteEntityIntention), typeof(PBTriggerArea), typeof(SDKTriggerAreaComponent))]
        private void HandleEntityDestruction(Entity entity)
        {
            // OnExitedArea();
            // activeAreas.Remove(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBTriggerArea))]
        [All(typeof(SDKTriggerAreaComponent))]
        private void HandleComponentRemoval(Entity entity)
        {
            // OnExitedArea();
            // activeAreas.Remove(entity);
            World.Remove<SDKTriggerAreaComponent>(entity);
        }

        [Query]
        [All(typeof(SDKTriggerAreaComponent))]
        private void FinalizeComponents(Entity entity)
        {
            // if (activeAreas.Remove(entity))
            //     OnExitedArea();

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



