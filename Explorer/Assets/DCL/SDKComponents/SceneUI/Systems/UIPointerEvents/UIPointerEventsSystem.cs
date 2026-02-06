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
            ConfigureHoverStylesBehaviourQuery(World);
            TriggerPointerEventsQuery(World);

            HandleEntityDestructionQuery(World);
            HandleUIPointerEventsRemovalQuery(World);
        }

        /*
         * The SDK lets creators put a UI Pointer Events on any UiEntity regardless of the rest of its components...
         * As defined in the SDK, UiButton entities composition breakdown:
         * https://github.com/decentraland/js-sdk-toolchain/blob/main/packages/@dcl/react-ecs/src/components/Button/index.tsx#L80-L90
         * - (mandatory) UiText
         * - (optional, but Explorer queries require it) PBUiTransform
         * - (optional) PBUiBackground
         * - (optional, but the Explorer queries require it) PBPointerEvents
         */
        [Query]
        [None(typeof(PBUiDropdown), typeof(PBUiInput), typeof(UiButtonComponent))]
        [All(typeof(PBPointerEvents), typeof(PBUiText))]
        private void ConfigureHoverStylesBehaviour(Entity entity, ref UITransformComponent uiTransformComponent)
        {
            UiElementUtils.ConfigureHoverStylesBehaviour(World, entity, uiTransformComponent.Transform, 0.22f, 0.1f);
            World.Add<UiButtonComponent>(entity);
        }

        [Query]
        private void TriggerPointerEvents(ref PBPointerEvents sdkModel, ref UITransformComponent uiTransformComponent, ref CRDTEntity sdkEntity)
        {
            if (sdkModel.IsDirty)
                uiTransformComponent.RegisterPointerCallbacks();

            sdkModel.IsDirty = false;

            if (uiTransformComponent.PointerEventTriggered == null)
                return;

            // Check if the component has any pointer events associated
            foreach (var pEvent in sdkModel.PointerEvents)
            {
                if (pEvent.EventType != uiTransformComponent.PointerEventTriggered)
                    continue;

                AppendMessage(ref sdkEntity, pEvent.EventInfo.Button, pEvent.EventType);
                break;
            }

            uiTransformComponent.PointerEventTriggered = null;
        }

        [Query]
        [None(typeof(PBPointerEvents), typeof(DeleteEntityIntention))]
        private void HandleUIPointerEventsRemoval(ref UITransformComponent uiTransformComponent, ref PBUiTransform sdkModel) =>
            RemovePointerEvents(ref uiTransformComponent, ref sdkModel);

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref UITransformComponent uiTransformComponent, ref PBUiTransform sdkModel) =>
            RemovePointerEvents(ref uiTransformComponent, ref sdkModel);

        private void AppendMessage(ref CRDTEntity sdkEntity, InputAction button, PointerEventType eventType)
        {
            ecsToCRDTWriter.AppendMessage<PBPointerEventsResult, (RaycastHit sdkHit, InputAction button, PointerEventType eventType, ISceneStateProvider sceneStateProvider)>(
                static (result, data) =>
                {
                    result.Hit = data.sdkHit;
                    result.Button = data.button;
                    result.State = data.eventType;
                    result.Timestamp = data.sceneStateProvider.TickNumber;
                    result.TickNumber = data.sceneStateProvider.TickNumber;
                }, sdkEntity, (int)sceneStateProvider.TickNumber, (null, button, eventType, sceneStateProvider));
        }

        private void RemovePointerEvents(ref UITransformComponent uiTransformComponent, ref PBUiTransform sdkModel)
        {
            uiTransformComponent.Transform.pickingMode = sdkModel.PointerFilter == PointerFilterMode.PfmBlock ? PickingMode.Position : PickingMode.Ignore;
            uiTransformComponent.UnregisterPointerCallbacks();
        }
    }
}
