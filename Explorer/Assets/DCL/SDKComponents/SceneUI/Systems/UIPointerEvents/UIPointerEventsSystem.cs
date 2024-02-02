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
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        public UIPointerEventsSystem(World world, ISceneStateProvider sceneStateProvider, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        protected override void Update(float _)
        {
            UpdatePointerEventsQuery(World);
        }

        [Query]
        private void UpdatePointerEvents(ref PBPointerEvents sdkModel, ref UITransformComponent uiTransformComponent, CRDTEntity sdkEntity)
        {
            if (!sdkModel.IsDirty)
                return;

            foreach (var pEvent in sdkModel.PointerEvents)
            {
                switch (pEvent.EventType)
                {
                    case PointerEventType.PetDown:
                        uiTransformComponent.Transform.RegisterPointerDownCallback(_ => AppendMessage(sdkEntity, pEvent.EventInfo.Button, PointerEventType.PetDown));
                        break;
                    case PointerEventType.PetUp:
                        uiTransformComponent.Transform.RegisterPointerUpCallback(_ => AppendMessage(sdkEntity, pEvent.EventInfo.Button, PointerEventType.PetUp));
                        break;
                }
            }

            uiTransformComponent.Transform.VisualElement.pickingMode = PickingMode.Position;
            sdkModel.IsDirty = false;
        }

        private void AppendMessage(CRDTEntity sdkEntity, InputAction button, PointerEventType eventType)
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
    }
}
