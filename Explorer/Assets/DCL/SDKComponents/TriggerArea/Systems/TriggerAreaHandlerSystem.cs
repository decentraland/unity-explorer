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
using Quaternion = Decentraland.Common.Quaternion;
using Vector3 = Decentraland.Common.Vector3;

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
            public readonly Vector3 TriggeredEntityPosition;
            public readonly Quaternion TriggeredEntityRotation;
            public readonly uint TriggerEntity;
            public readonly uint TriggerLayers;
            public readonly Vector3 TriggerEntityPosition;
            public readonly Quaternion TriggerEntityRotation;
            public readonly Vector3 TriggerEntityScale;

            public ResultData(
                TriggerAreaEventType eventType, uint triggeredEntity, uint timestamp,
                Vector3 triggeredEntityPosition, Quaternion triggeredEntityRotation,
                uint triggerEntity, uint triggerLayers,
                Vector3 triggerEntityPosition, Quaternion triggerEntityRotation,
                Vector3 triggerEntityScale)
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
            ColliderLayer mask = pbTriggerArea.GetColliderLayer();

            // Fast-path: mask is EXACTLY CL_MAIN_PLAYER — SDKEntityTriggerArea.OnTriggerEnter
            // early-outs on any collider that isn't the local player.
            bool targetOnlyMainPlayer = mask == ColliderLayer.ClMainPlayer;

            World.Add(
                entity,
                new SDKEntityTriggerAreaComponent(
                    areaSize: UnityEngine.Vector3.zero,
                    targetOnlyMainPlayer: targetOnlyMainPlayer,
                    meshType: (SDKEntityTriggerAreaMeshType)pbTriggerArea.GetMeshType(),
                    layerMask: mask),
                new TriggerAreaComponent()
            );
        }

        [Query]
        [All(typeof(PBTriggerArea), typeof(TriggerAreaComponent))]
        private void UpdateTriggerArea(Entity entity, in CRDTEntity triggerAreaCRDTEntity, in TransformComponent transform, ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            ProcessOnEnterTriggerArea(entity, triggerAreaCRDTEntity, transform, ref triggerAreaComponent);

            // TAET_STAY is intentionally not emitted on the wire. The SDK runtime synthesizes
            // per-tick OnStay callbacks locally from ENTER/EXIT events to avoid flooding the
            // GOVS-capped TriggerAreaResult buffer with redundant per-frame messages.
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
            int colliderLayer = triggerEntityCollider.gameObject.layer;
            bool isMainAvatar = colliderLayer == PhysicsLayers.CHARACTER_LAYER;
            bool isRemoteAvatar = colliderLayer == PhysicsLayers.OTHER_AVATARS_LAYER;
            if (isMainAvatar || isRemoteAvatar)
            {
                // Additive: main player matches (CL_PLAYER | CL_MAIN_PLAYER); remote avatars match CL_PLAYER only.
                ColliderLayer expected = isMainAvatar
                    ? PhysicsLayers.PLAYER_QUALIFYING_BITS
                    : ColliderLayer.ClPlayer;
                if ((areaLayerMask & expected) == 0
                    || !TryGetAvatarEntity(triggerEntityCollider.transform, out avatarEntity))
                    return;
            }

            Vector3 triggerEntityPos;
            Quaternion triggerEntityRot;
            Vector3 triggerEntityScale;
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

            // Report the intersection of the area mask with the bits the avatar carries.
            ColliderLayer triggerLayers;
            if (avatarEntity == Entity.Null)
                triggerLayers = entityInfo.SDKLayer;
            else if (isMainAvatar)
                triggerLayers = PhysicsLayers.PLAYER_QUALIFYING_BITS & areaLayerMask;
            else
                triggerLayers = ColliderLayer.ClPlayer & areaLayerMask;

            uint triggerEntity;
            if (avatarEntity != Entity.Null)
                triggerEntity = globalWorld.TryGet(avatarEntity, out PlayerCRDTEntity playerCrdtEntityComp) ? (uint)playerCrdtEntityComp.CRDTEntity.Id : 999999;
            else
                triggerEntity = (uint)entityInfo.SDKEntity.Id;

            var data = new ResultData(
                eventType, (uint)triggerAreaCRDTEntity.Id, incrementalTick,
                triggerAreaTransform.localPosition.ToProtoVector(), triggerAreaTransform.localRotation.ToProtoQuaternion(),
                triggerEntity, (uint)triggerLayers, triggerEntityPos, triggerEntityRot, triggerEntityScale);

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
