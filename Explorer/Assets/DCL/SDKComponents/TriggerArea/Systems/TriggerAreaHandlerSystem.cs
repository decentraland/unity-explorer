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
using DCL.SDKComponents.TriggerArea.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using UnityEngine;
using Utility;
using DCL.AvatarRendering.AvatarShape;
using DCL.Multiplayer.SDK.Components;

namespace DCL.SDKComponents.TriggerArea.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    [LogCategory(ReportCategory.CHARACTER_TRIGGER_AREA)]
    public partial class TriggerAreaHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        /// <summary>
        ///     Value-type snapshot of all result data passed into the CRDT writer closure,
        ///     avoiding shared pooled references that could be overwritten before deferred serialization.
        /// </summary>
        internal readonly struct ResultData
        {
            public readonly TriggerAreaEventType EventType;
            public readonly uint TriggeredEntity;
            public readonly uint Timestamp;
            public readonly Decentraland.Common.Vector3 TriggeredEntityPosition;
            public readonly Decentraland.Common.Quaternion TriggeredEntityRotation;
            public readonly uint TriggerEntity;
            public readonly uint TriggerLayers;
            public readonly Decentraland.Common.Vector3 TriggerEntityPosition;
            public readonly Decentraland.Common.Quaternion TriggerEntityRotation;
            public readonly Decentraland.Common.Vector3 TriggerEntityScale;

            public ResultData(
                TriggerAreaEventType eventType, uint triggeredEntity, uint timestamp,
                Decentraland.Common.Vector3 triggeredEntityPosition, Decentraland.Common.Quaternion triggeredEntityRotation,
                uint triggerEntity, uint triggerLayers,
                Decentraland.Common.Vector3 triggerEntityPosition, Decentraland.Common.Quaternion triggerEntityRotation,
                Decentraland.Common.Vector3 triggerEntityScale)
            {
                EventType = eventType;
                TriggeredEntity = triggeredEntity;
                Timestamp = timestamp;
                TriggeredEntityPosition = triggeredEntityPosition;
                TriggeredEntityRotation = triggeredEntityRotation;
                TriggerEntity = triggerEntity;
                TriggerLayers = triggerLayers;
                TriggerEntityPosition = triggerEntityPosition;
                TriggerEntityRotation = triggerEntityRotation;
                TriggerEntityScale = triggerEntityScale;
            }
        }

        private readonly World globalWorld;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly IEntityCollidersSceneCache collidersSceneCache;
        private readonly ISceneData sceneData;

        public TriggerAreaHandlerSystem(
            World world,
            World globalWorld,
            IECSToCRDTWriter ecsToCRDTWriter,
            IEntityCollidersSceneCache collidersSceneCache,
            ISceneData sceneData) : base(world)
        {
            this.globalWorld = globalWorld;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.collidersSceneCache = collidersSceneCache;
            this.sceneData = sceneData;
        }

        protected override void Update(float t)
        {
            HandleEntityDestructionQuery(World);
            HandleComponentRemovalQuery(World);

            SetupTriggerAreaQuery(World);
            UpdateTriggerAreaQuery(World);
        }

        [Query]
        [None(typeof(SDKEntityTriggerAreaComponent), typeof(TriggerAreaComponent))]
        [All(typeof(TransformComponent))]
        private void SetupTriggerArea(Entity entity, in PBTriggerArea pbTriggerArea)
        {
            World.Add(
                entity,
                new SDKEntityTriggerAreaComponent(
                    areaSize: Vector3.zero,
                    targetOnlyMainPlayer: false,
                    meshType: (SDKEntityTriggerAreaMeshType)pbTriggerArea.GetMeshType(),
                    layerMask: pbTriggerArea.GetColliderLayer()),
                new TriggerAreaComponent()
            );
        }

        [Query]
        [All(typeof(PBTriggerArea), typeof(TriggerAreaComponent))]
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
                    entityCollider, TriggerAreaEventType.TaetEnter, triggerAreaComponent.LayerMask, triggerAreaComponent.IncrementalTick);
            }
            triggerAreaComponent.TryClearEnteredAvatarsToBeProcessed();
        }

        private void ProcessOnStayInTriggerArea(in Entity triggerAreaEntity, in CRDTEntity triggerAreaCRDTEntity, in TransformComponent transform, ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            foreach (Collider entityCollider in triggerAreaComponent.CurrentEntitiesInside)
            {
                PropagateResultComponent(triggerAreaEntity, triggerAreaCRDTEntity, transform.Transform,
                    entityCollider, TriggerAreaEventType.TaetStay, triggerAreaComponent.LayerMask, triggerAreaComponent.IncrementalTick);
            }
        }

        private void ProcessOnExitTriggerArea(in Entity triggerAreaEntity, in CRDTEntity triggerAreaCRDTEntity, in TransformComponent transform, ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            foreach (Collider entityCollider in triggerAreaComponent.ExitedEntitiesToBeProcessed)
            {
                PropagateResultComponent(triggerAreaEntity, triggerAreaCRDTEntity, transform.Transform,
                    entityCollider, TriggerAreaEventType.TaetExit, triggerAreaComponent.LayerMask, triggerAreaComponent.IncrementalTick);
            }
            triggerAreaComponent.TryClearExitedAvatarsToBeProcessed();
        }

        private void PropagateResultComponent(in Entity triggerAreaEntity, in CRDTEntity triggerAreaCRDTEntity, Transform triggerAreaTransform,
            Collider triggerEntityCollider, TriggerAreaEventType eventType, ColliderLayer areaLayerMask, uint incrementalTick)
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

            Decentraland.Common.Vector3 triggerEntityPos;
            Decentraland.Common.Quaternion triggerEntityRot;
            Decentraland.Common.Vector3 triggerEntityScale;
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

            uint triggerLayers = avatarEntity == Entity.Null ? (uint)entityInfo.SDKLayer : (uint)ColliderLayer.ClPlayer;

            uint triggerEntity;
            if (avatarEntity != Entity.Null)
                triggerEntity = globalWorld.TryGet(avatarEntity, out PlayerCRDTEntity playerCrdtEntityComp) ? (uint)playerCrdtEntityComp.CRDTEntity.Id : 999999;
            else
                triggerEntity = (uint)entityInfo.SDKEntity.Id;

            var data = new ResultData(
                eventType, (uint)triggerAreaCRDTEntity.Id, incrementalTick,
                triggerAreaTransform.localPosition.ToProtoVector(), triggerAreaTransform.localRotation.ToProtoQuaternion(),
                triggerEntity, triggerLayers, triggerEntityPos, triggerEntityRot, triggerEntityScale);

            ecsToCRDTWriter.AppendMessage<PBTriggerAreaResult, ResultData>
            (
                prepareMessage: static (pbTriggerAreaResult, data) =>
                {
                    pbTriggerAreaResult.EventType = data.EventType;
                    pbTriggerAreaResult.TriggeredEntity = data.TriggeredEntity;
                    pbTriggerAreaResult.Timestamp = data.Timestamp;
                    pbTriggerAreaResult.TriggeredEntityPosition = data.TriggeredEntityPosition;
                    pbTriggerAreaResult.TriggeredEntityRotation = data.TriggeredEntityRotation;

                    pbTriggerAreaResult.Trigger ??= new PBTriggerAreaResult.Types.Trigger();
                    pbTriggerAreaResult.Trigger.Entity = data.TriggerEntity;
                    pbTriggerAreaResult.Trigger.Layers = data.TriggerLayers;
                    pbTriggerAreaResult.Trigger.Position = data.TriggerEntityPosition;
                    pbTriggerAreaResult.Trigger.Rotation = data.TriggerEntityRotation;
                    pbTriggerAreaResult.Trigger.Scale = data.TriggerEntityScale;
                },
                triggerAreaCRDTEntity, (int)incrementalTick, data
            );
        }

        [Query]
        [All(typeof(DeleteEntityIntention), typeof(PBTriggerArea))]
        private void HandleEntityDestruction(Entity entity, in CRDTEntity triggerAreaCRDTEntity, in TransformComponent transform, ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            ProcessOnExitTriggerArea(entity, triggerAreaCRDTEntity, transform, ref triggerAreaComponent);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBTriggerArea))]
        [All(typeof(TriggerAreaComponent))]
        private void HandleComponentRemoval(Entity entity, in CRDTEntity triggerAreaCRDTEntity, in TransformComponent transform, ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            ProcessOnExitTriggerArea(entity, triggerAreaCRDTEntity, transform, ref triggerAreaComponent);
            World.Remove<TriggerAreaComponent>(entity);
        }

        [Query]
        [All(typeof(TriggerAreaComponent))]
        private void FinalizeComponents(Entity entity, in CRDTEntity triggerAreaCRDTEntity, in TransformComponent transform, ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            ProcessOnExitTriggerArea(entity, triggerAreaCRDTEntity, transform, ref triggerAreaComponent);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }

        protected override void OnDispose() { }

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