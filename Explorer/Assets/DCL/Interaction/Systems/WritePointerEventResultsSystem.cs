using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using RaycastHit = DCL.ECSComponents.RaycastHit;

namespace DCL.Interaction.PlayerOriginated.Systems
{
    /// <summary>
    ///     Writes the results of the pointer events in the scene world: raycast hits on world entities, pointer events
    ///     UI Toolkit dispatched on scene UI elements, and the global events of the frame.
    ///     <para>
    ///         Every result of a frame is written here in one pass with one tick number, so the world, UI and global
    ///         results of the same press share a timestamp.
    ///     </para>
    ///     <para>
    ///         Must be executed after <see cref="Interaction.Systems.ProcessPointerEventsSystem" />. As they exist in different worlds we must attach them to different
    ///         root system groups as we can't make dependencies between them directly
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class WritePointerEventResultsSystem : BaseUnityLoopSystem
    {
        private readonly ISceneData sceneData;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IGlobalInputEvents globalInputEvents;

        private readonly IComponentPool<RaycastHit> raycastHitPool;

        internal WritePointerEventResultsSystem(
            World world,
            ISceneData sceneData,
            IECSToCRDTWriter ecsToCRDTWriter,
            ISceneStateProvider sceneStateProvider,
            IGlobalInputEvents globalInputEvents,
            IComponentPool<RaycastHit> raycastHitPool
        ) : base(world)
        {
            this.sceneData = sceneData;
            this.ecsToCRDTWriter = ecsToCRDTWriter;

            this.sceneStateProvider = sceneStateProvider;
            this.globalInputEvents = globalInputEvents;
            this.raycastHitPool = raycastHitPool;
        }

        protected override void Update(float t)
        {
            // The scene thread advances the tick concurrently: read it once so every result of this frame shares it.
            uint tick = sceneStateProvider.TickNumber;

            // UI events are written regardless of the scene being current: an event that stayed queued while the scene
            // was not current would otherwise be delivered later as a fresh user gesture.
            WriteUiResultsQuery(World!, tick);

            if (!sceneStateProvider.IsCurrent) return;

            var messageSent = false;
            WriteResultsQuery(World!, sceneData.Geometry.BaseParcelPosition, tick, ref messageSent);

            if (!messageSent)
                WriteGlobalEvents(tick);
        }

        private void WriteGlobalEvents(uint tick)
        {
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < globalInputEvents.Entries.Count; i++)
            {
                IGlobalInputEvents.Entry entry = globalInputEvents.Entries[i];
                AppendMessage(SpecialEntitiesID.SCENE_ROOT_ENTITY, null, entry.InputAction, entry.PointerEventType, tick);
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void WriteResults([Data] in Vector3 scenePosition, [Data] uint tick, [Data] ref bool messageSent, ref PBPointerEvents pbPointerEvents, ref CRDTEntity sdkEntity)
        {
            AppendPointerEventResultsIntent intent = pbPointerEvents.AppendPointerEventResultsIntent;
            int validIndicesCount = intent.ValidIndicesCount();

            for (var i = 0; i < validIndicesCount; i++)
            {
                byte validIndex = intent.ValidIndexAt(i);
                PBPointerEvents.Types.Entry entry = pbPointerEvents.PointerEvents![validIndex]!;
                PBPointerEvents.Types.Info info = entry.EventInfo!;

                //Note: If the event is a Hover, the scenes are expecting an input action of type IaPointer.
                //This logic is extracted from previous renderer in ECSPointerInputSystem.
                InputAction inputAction = entry.EventType is PointerEventType.PetHoverEnter or PointerEventType.PetHoverLeave ? InputAction.IaPointer : info.Button;
                AppendSingleResult(sdkEntity, scenePosition, intent, inputAction, entry.EventType, tick);
            }

            if (intent.ValidInputActions != null)
            {
                for (var i = 0; i < intent.ValidInputActions.Count; i++)
                {
                    (InputAction inputAction, PointerEventType pointerEventType) entry = intent.ValidInputActions[i];
                    messageSent |= AppendSingleResult(sdkEntity, scenePosition, intent, entry.inputAction, entry.pointerEventType, tick);
                }
            }

            intent.Clear();
        }

        /// <summary>
        ///     Writes, in dispatch order, every pointer event UI Toolkit queued on the element since the last frame that
        ///     the element's <see cref="PBPointerEvents" /> listens to. UI events do not suppress the global events.
        /// </summary>
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void WriteUiResults([Data] uint tick, ref PBPointerEvents pbPointerEvents, ref UITransformComponent uiTransformComponent, ref CRDTEntity sdkEntity)
        {
            List<PointerEventType> pendingEvents = uiTransformComponent.PendingPointerEvents;

            for (var i = 0; i < pendingEvents.Count; i++)
            {
                PointerEventType eventType = pendingEvents[i];

                for (var j = 0; j < pbPointerEvents.PointerEvents.Count; j++)
                {
                    PBPointerEvents.Types.Entry entry = pbPointerEvents.PointerEvents[j];

                    if (entry.EventType != eventType)
                        continue;

                    AppendMessage(sdkEntity, null, entry.EventInfo.Button, eventType, tick);
                    break;
                }
            }

            pendingEvents.Clear();
        }

        /// <summary>
        /// Appends one pointer event result. Returns true if the event should suppress global input (non-hover).
        /// </summary>
        private bool AppendSingleResult(CRDTEntity sdkEntity, in Vector3 scenePosition, in AppendPointerEventResultsIntent intent, InputAction button, PointerEventType eventType, uint tick)
        {
            RaycastHit raycastHit = raycastHitPool.Get();
            raycastHit.FillSDKRaycastHit(scenePosition, intent, sdkEntity);
            AppendMessage(sdkEntity, raycastHit, button, eventType, tick);

            return eventType is not (PointerEventType.PetHoverEnter or PointerEventType.PetHoverLeave);
        }

        private void AppendMessage(CRDTEntity sdkEntity, RaycastHit? sdkHit, InputAction button, PointerEventType eventType, uint tick)
        {
            if (IsUserGesture(button, eventType))
                sceneStateProvider.LastUserInputTick = tick;

            ecsToCRDTWriter.AppendMessage<PBPointerEventsResult, (RaycastHit? sdkHit, InputAction button, PointerEventType eventType, uint tick)>(
                static (result, data) =>
                {
                    result.Hit = data.sdkHit;
                    result.Button = data.button;
                    result.State = data.eventType;
                    result.Timestamp = data.tick;
                    result.TickNumber = data.tick;
                }, sdkEntity, (int)tick, (sdkHit, button, eventType, tick));
        }

        /// <summary>
        ///     Only a press or release of an activation button counts: hover, proximity, movement, jump, walk, modifier and any-key input do not express intent
        /// </summary>
        private static bool IsUserGesture(InputAction button, PointerEventType eventType) =>
            eventType is PointerEventType.PetDown or PointerEventType.PetUp
            && button is InputAction.IaPointer or InputAction.IaPrimary or InputAction.IaSecondary
                or InputAction.IaAction3 or InputAction.IaAction4 or InputAction.IaAction5 or InputAction.IaAction6;
    }
}
