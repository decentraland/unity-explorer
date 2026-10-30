using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UITransform;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using SceneRunner.Scene;
using UnityEngine.UIElements;
using RaycastHit = DCL.ECSComponents.RaycastHit;

namespace DCL.SDKComponents.SceneUI.Systems.UIPointerEvents
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(UITransformUpdateSystem))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UIPointerEventsSystem : BaseUnityLoopSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        public UIPointerEventsSystem(World world, ISceneStateProvider sceneStateProvider, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        protected override void Update(float _)
        {
            TriggerPointerEventsQuery(World);

            HandleEntityDestructionQuery(World);
            HandleUiPointerEventsRemovalQuery(World);
        }

        [Query]
        private void TriggerPointerEvents(ref PBPointerEvents sdkModel, ref UITransformComponent uiTransformComponent, ref CRDTEntity sdkEntity)
        {
            if (sdkModel.IsDirty)
                uiTransformComponent.RegisterPointerCallbacks();

            sdkModel.IsDirty = false;

            if (uiTransformComponent.PointerEventTriggered == null)
                return;

            PointerEventType triggered = uiTransformComponent.PointerEventTriggered.Value;
            var appended = false;

            // Check if the component has any pointer events associated
            foreach (var pEvent in sdkModel.PointerEvents)
            {
                if (pEvent.EventType != triggered)
                    continue;

                AppendMessage(ref sdkEntity, pEvent.EventInfo.Button, pEvent.EventType);
                appended = true;
                break;
            }

            // The slot is cleared either way: an event the entity did not declare is dropped, and the log says so.
            uiTransformComponent.Drains.Record(triggered, appended);
            uiTransformComponent.PointerEventTriggered = null;
        }

        [Query]
        [None(typeof(PBPointerEvents), typeof(DeleteEntityIntention))]
        private void HandleUiPointerEventsRemoval(ref UITransformComponent uiTransformComponent, in PBUiTransform sdkModel) =>
            RemovePointerEvents(ref uiTransformComponent, in sdkModel);

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref UITransformComponent uiTransformComponent, in PBUiTransform sdkModel) =>
            RemovePointerEvents(ref uiTransformComponent, in sdkModel);

        private void AppendMessage(ref CRDTEntity sdkEntity, InputAction button, PointerEventType eventType)
        {
            if (eventType is not (PointerEventType.PetHoverEnter or PointerEventType.PetHoverLeave))
                sceneStateProvider.LastUserInputTick = sceneStateProvider.TickNumber;

            ecsToCRDTWriter.AppendMessage<PBPointerEventsResult, (RaycastHit? sdkHit, InputAction button, PointerEventType eventType, ISceneStateProvider sceneStateProvider)>(
                static (result, data) =>
                {
                    result.Hit = data.sdkHit;
                    result.Button = data.button;
                    result.State = data.eventType;
                    result.Timestamp = data.sceneStateProvider.TickNumber;
                    result.TickNumber = data.sceneStateProvider.TickNumber;
                }, sdkEntity, (int)sceneStateProvider.TickNumber, (null, button, eventType, sceneStateProvider));
        }

        private void RemovePointerEvents(ref UITransformComponent uiTransformComponent, in PBUiTransform sdkModel)
        {
            uiTransformComponent.Transform.pickingMode = sdkModel.PointerFilter == PointerFilterMode.PfmBlock ? PickingMode.Position : PickingMode.Ignore;
            uiTransformComponent.UnregisterPointerCallbacks();
            uiTransformComponent.Transform.UnregisterHoverStyleCallbacks();
        }
    }
}
