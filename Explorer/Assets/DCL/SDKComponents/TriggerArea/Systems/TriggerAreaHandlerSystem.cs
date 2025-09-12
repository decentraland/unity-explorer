using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.CharacterTriggerArea.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
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

        public TriggerAreaHandlerSystem(
            World world,
            World globalWorld,
            IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap,
            IECSToCRDTWriter ecsToCRDTWriter,
            IComponentPool<PBTriggerAreaResult> triggerAreaResultPool,
            ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.globalWorld = globalWorld;
            this.entitiesMap = entitiesMap;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.triggerAreaResultPool = triggerAreaResultPool;
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float t)
        {
            SetupTriggerAreaQuery(World);
            UpdateTriggerAreaQuery(World);
        }

        [Query]
        [None(typeof(CharacterTriggerAreaComponent), typeof(SDKTriggerAreaComponent))]
        [All(typeof(TransformComponent))]
        private void SetupTriggerArea(Entity entity, in PBTriggerArea pbTriggerArea)
        {
            // TODO: make the CharacterTriggerAreaComponent more versatile, to accept scene entity colliders
            // TODO: make CharacterTriggerAreaComponent configurable to use BOX or SPHERE
            World.Add(entity, new SDKTriggerAreaComponent(), new CharacterTriggerAreaComponent(areaSize: Vector3.zero, targetOnlyMainPlayer: false));
        }

        [Query]
        [All(typeof(TransformComponent))]
        private void UpdateTriggerArea(in CRDTEntity triggerAreaCRDTEntity, in PBTriggerArea pbTriggerArea, in CharacterTriggerAreaComponent triggerAreaComponent)
        {
            foreach (Transform avatarTransform in triggerAreaComponent.EnteredAvatarsToBeProcessed)
            {
                if (!TryGetAvatarEntity(avatarTransform, out var entity)) continue;

                // TODO...
                var resultComponent = triggerAreaResultPool.Get();
                resultComponent.EventType = TriggerAreaEventType.TaetEnter;
                resultComponent.TriggeredEntity = (uint)triggerAreaCRDTEntity.Id;
                // resultComponent.Timestamp =
                // resultComponent.TriggeredEntityPosition =
                // resultComponent.TriggeredEntityRotation =
                /*resultComponent.Trigger = new PBTriggerAreaResult.Types.Trigger()
                {
                    Entity = ,
                    Layer = ,
                    Position = ,
                    Rotation = ,
                    Scale =
                }*/

                PropagateResultComponent(triggerAreaCRDTEntity, resultComponent);
            }
            triggerAreaComponent.TryClearEnteredAvatarsToBeProcessed();

            // TODO: STAY...
            // TODO: Can we infer the "STAY" state when ENTER was received but not EXIT ???

            foreach (Transform avatarTransform in triggerAreaComponent.ExitedAvatarsToBeProcessed)
            {
                if (!TryGetAvatarEntity(avatarTransform, out var entity)) continue;

                // TODO...
                var resultComponent = triggerAreaResultPool.Get();
                resultComponent.EventType = TriggerAreaEventType.TaetExit;
                resultComponent.TriggeredEntity = (uint)triggerAreaCRDTEntity.Id;
                // resultComponent.Timestamp =
                // resultComponent.TriggeredEntityPosition =
                // resultComponent.TriggeredEntityRotation =
                /*resultComponent.Trigger = new PBTriggerAreaResult.Types.Trigger()
                {
                    Entity = ,
                    Layer = ,
                    Position = ,
                    Rotation = ,
                    Scale =
                }*/

                PropagateResultComponent(triggerAreaCRDTEntity, resultComponent);
            }
            triggerAreaComponent.TryClearExitedAvatarsToBeProcessed();
        }

        private void PropagateResultComponent(in CRDTEntity triggerAreaCRDTEntity, in PBTriggerAreaResult resultComponent)
        {
            ecsToCRDTWriter.AppendMessage<PBTriggerAreaResult, (PBTriggerAreaResult result, uint timestamp)>
            (
                prepareMessage: static (pbTriggerAreaResult, data) =>
                {
                    pbTriggerAreaResult.TriggeredEntity = data.result.TriggeredEntity;
                    pbTriggerAreaResult.EventType = data.result.EventType;
                    pbTriggerAreaResult.TriggeredEntityRotation = data.result.TriggeredEntityRotation;
                    pbTriggerAreaResult.TriggeredEntityPosition = data.result.TriggeredEntityPosition;
                    pbTriggerAreaResult.Trigger = data.result.Trigger;

                    pbTriggerAreaResult.Timestamp = data.timestamp;
                },
                triggerAreaCRDTEntity, (int)sceneStateProvider.TickNumber, (resultComponent, sceneStateProvider.TickNumber)
            );
        }

        [Query]
        [All(typeof(DeleteEntityIntention), typeof(PBTriggerArea), typeof(SDKTriggerAreaComponent))]
        private void HandleEntityDestruction(Entity entity)
        {
            OnExitedArea();
            // activeAreas.Remove(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBTriggerArea))]
        [All(typeof(SDKTriggerAreaComponent))]
        private void HandleComponentRemoval(Entity entity)
        {
            OnExitedArea();
            // activeAreas.Remove(entity);
            World.Remove<SDKTriggerAreaComponent>(entity);
        }

        internal void OnEnteredArea()
        {
            // if (globalWorld.Has<InWorldCameraComponent>(cameraEntityProxy.Object))
            //     globalWorld.Add(cameraEntityProxy.Object, new ToggleInWorldCameraRequest { IsEnable = false, TargetCameraMode = targetCameraMode});
            //
            // ref CameraComponent camera = ref globalWorld.Get<CameraComponent>(cameraEntityProxy.Object!);
            //
            // cameraModeBeforeLastAreaEnter = camera.Mode;
            // camera.Mode = targetCameraMode;
            // camera.AddCameraInputLock();
            //
            // sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateCameraLocked(SceneRestrictionsAction.APPLIED));
        }

        internal void OnExitedArea()
        {
            // ref CameraComponent camera = ref globalWorld.Get<CameraComponent>(cameraEntityProxy.Object!);
            //
            // camera.RemoveCameraInputLock();
            //
            // // If there are more locks then there is another newer camera mode area in place
            // if (camera.CameraInputChangeEnabled)
            //     camera.Mode = cameraModeBeforeLastAreaEnter == CameraMode.InWorld? CameraMode.ThirdPerson : cameraModeBeforeLastAreaEnter;
            //
            // sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateCameraLocked(SceneRestrictionsAction.REMOVED));
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



