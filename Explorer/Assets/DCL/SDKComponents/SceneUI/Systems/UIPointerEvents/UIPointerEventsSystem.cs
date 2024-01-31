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
using ECS.Abstract;
using ECS.Groups;
using SceneRunner.Scene;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Systems.UIPointerEvents
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(UITransformUpdateSystem))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UIPointerEventsSystem : BaseUnityLoopSystem
    {
        private static readonly PBPointerEventsResult SHARED_POINTER_EVENTS_RESULT = new ();

        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        private UIPointerEventsSystem(World world, ISceneStateProvider sceneStateProvider, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        protected override void Update(float _)
        {
            CheckPointerEventsQuery(World);
        }

        [Query]
        private void CheckPointerEvents(ref PBPointerEvents pointerEventsModel, ref UITransformComponent uiTransformComponent, CRDTEntity sdkEntity)
        {
            if (!pointerEventsModel.IsDirty)
                return;

            foreach (var pEvent in pointerEventsModel.PointerEvents)
            {
                switch (pEvent.EventType)
                {
                    case PointerEventType.PetDown:
                        EventCallback<PointerDownEvent> onPointerDownCallback = null;
                        onPointerDownCallback += _ => AppendMessage(sdkEntity, pEvent.EventInfo.Button, PointerEventType.PetDown);
                        uiTransformComponent.Transform.UnregisterCallback(onPointerDownCallback);
                        uiTransformComponent.Transform.RegisterCallback(onPointerDownCallback);
                        break;
                    case PointerEventType.PetUp:
                        EventCallback<PointerUpEvent> onPointerUpCallback = null;
                        onPointerUpCallback += _ => AppendMessage(sdkEntity, pEvent.EventInfo.Button, PointerEventType.PetUp);
                        uiTransformComponent.Transform.UnregisterCallback(onPointerUpCallback);
                        uiTransformComponent.Transform.RegisterCallback(onPointerUpCallback);
                        break;
                }
            }

            uiTransformComponent.Transform.pickingMode = PickingMode.Position;
            pointerEventsModel.IsDirty = false;
        }

        private void AppendMessage(CRDTEntity sdkEntity, InputAction button, PointerEventType eventType)
        {
            PBPointerEventsResult result = SHARED_POINTER_EVENTS_RESULT;
            result.Hit = null;
            result.Button = button;
            result.State = eventType;
            result.Timestamp = sceneStateProvider.TickNumber;
            result.TickNumber = sceneStateProvider.TickNumber;

            ecsToCRDTWriter.AppendMessage(sdkEntity, result, (int)result.Timestamp);
        }
    }
}
