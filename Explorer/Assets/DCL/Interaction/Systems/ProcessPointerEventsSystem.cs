using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Input;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.PlayerOriginated.Utility;
using DCL.Interaction.Raycast.Components;
using DCL.Interaction.Utility;
using ECS.Abstract;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.Interaction.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PlayerOriginatedRaycastSystem))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class ProcessPointerEventsSystem : BaseUnityLoopSystem
    {
        private readonly IEntityCollidersGlobalCache entityCollidersGlobalCache;
        private readonly IEventSystem eventSystem;
        // Concrete Dictionary (not IReadOnlyDictionary): enumerating .Values / the map itself binds
        // the struct enumerators instead of boxing IEnumerator<> through the interface every hover
        // frame (see InteractionInputUtils.GatherAnyInputInfo / TryAppendButtonAction overloads).
        private readonly Dictionary<InputAction, UnityEngine.InputSystem.InputAction> sdkInputActionsMap;
        private readonly QueryDescription highlightQuery = new QueryDescription().WithAll<HighlightComponent>();

        // Records which scene Worlds already have their HighlightComponent tracker entity, so
        // HighlightNewEntity can skip the per-frame CountEntities(highlightQuery) archetype scan.
        // ConditionalWeakTable (not a Dictionary) because this system is a single global instance
        // that outlives every scene World it sees: the World is a WEAK key here, so a disposed scene
        // World is collected instead of being pinned for the app's lifetime, and membership is keyed
        // by reference identity — never by World.Id — so a later World that Arch hands a recycled Id
        // can never false-hit a departed scene's entry. Value is a shared sentinel (presence is all
        // that matters).
        private static readonly object TRACKER_PRESENT = new ();
        private readonly ConditionalWeakTable<World, object> highlightTrackerWorlds = new ();

        private SingleInstanceEntity playerCamera;

        internal ProcessPointerEventsSystem(World world,
            Dictionary<InputAction, UnityEngine.InputSystem.InputAction> sdkInputActionsMap,
            IEntityCollidersGlobalCache entityCollidersGlobalCache,
            IEventSystem eventSystem) : base(world)
        {
            this.sdkInputActionsMap = sdkInputActionsMap;
            this.entityCollidersGlobalCache = entityCollidersGlobalCache;

            this.eventSystem = eventSystem;
        }

        public override void Initialize()
        {
            playerCamera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            ProcessPointerEventsQuery(World!);
        }

        [Query]
        private void ProcessPointerEvents(ref PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities,
            ref ProximityResultForSceneEntities proximityResultForSceneEntities,
            ref HoverFeedbackComponent hoverFeedbackComponent,
            ref HoverStateComponent hoverStateComponent,
            ref SyntheticPointerInput syntheticPointerInput)
        {
            // Synthetic instructions apply to exactly this frame: a post that survived from an earlier frame
            // (this system did not run since it was made) is discarded unread, and consuming the component
            // here guarantees a driver that stops re-posting leaves no residue.
            SyntheticPointerInput synthetic = syntheticPointerInput.IsPostedThisFrame ? syntheticPointerInput : default(SyntheticPointerInput);
            syntheticPointerInput = default(SyntheticPointerInput);

            // Process all PBPointerEvents components to see if any of them is qualified
            hoverFeedbackComponent.Clear();
            bool candidateForHoverLeaveIsValid = TryGetPreviousEntityInfo(in hoverStateComponent, out GlobalColliderSceneEntityInfo previousEntityInfo);
            hoverStateComponent.Clear();

            if (
                TryGetInteractableEntity(
                    in raycastResultForSceneEntities,
                    in proximityResultForSceneEntities,
                    synthetic.AimPoint.HasValue,
                    out GlobalColliderSceneEntityInfo entityInfo,
                    out PBPointerEvents? pbPointerEvents,
                    out Collider? collider)
            )
            {
                bool newEntityIsSelected = NewEntityWasHovered(candidateForHoverLeaveIsValid, previousEntityInfo, entityInfo);

                // Signal to stop issuing hover leave event for the previous entity as it's equal to the current one
                if (candidateForHoverLeaveIsValid && newEntityIsSelected == false)
                    candidateForHoverLeaveIsValid = false;

                SetupPointerEvents(
                    entityInfo,
                    raycastResultForSceneEntities,
                    proximityResultForSceneEntities,
                    ref hoverFeedbackComponent,
                    pbPointerEvents!,
                    newEntityIsSelected,
                    in synthetic,
                    out bool isAtDistance);

                    hoverStateComponent.AssignCollider(collider!, isAtDistance, hoverFeedbackComponent.ScreenPositionOverride == null);
            }

            if (candidateForHoverLeaveIsValid)
                ResetPreviousEntity(in raycastResultForSceneEntities, in proximityResultForSceneEntities, in previousEntityInfo);
        }

        private bool TryGetPreviousEntityInfo(in HoverStateComponent stateComponent, out GlobalColliderSceneEntityInfo globalColliderSceneEntityInfo)
        {
            if (!stateComponent.LastHitCollider) // collider was destroyed, nothing to do
            {
                globalColliderSceneEntityInfo = default(GlobalColliderSceneEntityInfo);
                return false;
            }

            return entityCollidersGlobalCache.TryGetSceneEntity(stateComponent.LastHitCollider!, out globalColliderSceneEntityInfo); // scene was destroyed, collider was returned to the pool, nothing to do
        }

        private bool TryGetInteractableEntity(in PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities,
            in ProximityResultForSceneEntities proximityResultForSceneEntities,
            bool syntheticAim,
            out GlobalColliderSceneEntityInfo entityInfo,
            out PBPointerEvents? pbPointerEvents,
            out Collider? collider
        )
        {
            // Check cursor type first
            if (TryGetInteractableEntityFromCursor(
                    in raycastResultForSceneEntities,
                    syntheticAim,
                    out GlobalColliderSceneEntityInfo cursorEntityInfo,
                    out PBPointerEvents? cursorPointerEvents,
                    out Collider? cursorCollider))
            {
                entityInfo = cursorEntityInfo;
                pbPointerEvents = cursorPointerEvents;
                collider = cursorCollider;
                return true;
            }

            // Otherwise check proximity next
            if (TryGetInteractableEntityFromProximity(
                    in proximityResultForSceneEntities,
                    out GlobalColliderSceneEntityInfo proximityEntityInfo,
                    out PBPointerEvents? proximityPointerEvents,
                    out Collider? proximityCollider))
            {
                entityInfo = proximityEntityInfo;
                pbPointerEvents = proximityPointerEvents;
                collider = proximityCollider;
                return true;
            }

            entityInfo = default(GlobalColliderSceneEntityInfo);
            pbPointerEvents = null;
            collider = null;
            return false;
        }

        private bool TryGetInteractableEntityFromCursor(in PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities,
            bool syntheticAim,
            out GlobalColliderSceneEntityInfo entityInfo,
            out PBPointerEvents? pbPointerEvents,
            out Collider? cursorCollider)
        {
            if (
                IsPointingOnEntity(in raycastResultForSceneEntities, syntheticAim, out GlobalColliderSceneEntityInfo pointedEntityInfo)
                && pointedEntityInfo.TryGetPointerEvents(out PBPointerEvents? foundPointerEvents)
                && HasCursorEvent(foundPointerEvents!))
            {
                entityInfo = pointedEntityInfo;
                pbPointerEvents = foundPointerEvents;
                cursorCollider = raycastResultForSceneEntities.Collider;
                return true;
            }

            entityInfo = default(GlobalColliderSceneEntityInfo);
            pbPointerEvents = null;
            cursorCollider = null;
            return false;
        }

        private bool IsPointingOnEntity(in PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities, bool syntheticAim, out GlobalColliderSceneEntityInfo entityInfo)
        {
            // A synthetic ray does not originate from the OS cursor, so UI under the cursor cannot occlude it
            bool canHover = syntheticAim || eventSystem.IsPointerOverGameObject() == false;
            entityInfo = raycastResultForSceneEntities.EntityInfo ?? default(GlobalColliderSceneEntityInfo);
            return raycastResultForSceneEntities.IsValidHit && canHover && raycastResultForSceneEntities.EntityInfo != null;
        }

        private static bool HasCursorEvent(PBPointerEvents pointerEvents)
        {
            int count = pointerEvents.PointerEvents.Count;

            for (int i = 0; i < count; i++)
                if (pointerEvents.PointerEvents[i].InteractionType == InteractionType.Cursor)
                    return true;

            return false;
        }

        private bool TryGetInteractableEntityFromProximity(in ProximityResultForSceneEntities proximityResultForSceneEntities,
            out GlobalColliderSceneEntityInfo entityInfo,
            out PBPointerEvents? pbPointerEvents,
            out Collider? cursorCollider)
        {
            if (
                proximityResultForSceneEntities.EntityInfo.HasValue
                && proximityResultForSceneEntities.EntityInfo.Value.TryGetPointerEvents(out PBPointerEvents? pointerEvents)
            )
            {
                entityInfo = proximityResultForSceneEntities.EntityInfo.Value;
                pbPointerEvents = pointerEvents!;
                cursorCollider = proximityResultForSceneEntities.Collider!;
                return true;
            }

            entityInfo = default(GlobalColliderSceneEntityInfo);
            pbPointerEvents = null;
            cursorCollider = null;
            return false;
        }

        private void ResetPreviousEntity(
            in PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities,
            in ProximityResultForSceneEntities proximityResultForSceneEntities,
            in GlobalColliderSceneEntityInfo previousEntityInfo
        )
        {
            ResetHighlightComponentQuery(previousEntityInfo.EcsExecutor.World);
            HoverFeedbackUtils.TryIssueLeaveHoverEventForPreviousEntity(in raycastResultForSceneEntities, in previousEntityInfo);
            ProximityFeedbackUtils.TryIssueProximityLeaveEventForPreviousEntity(in proximityResultForSceneEntities, in previousEntityInfo);
        }

        // Test seam: count of HighlightComponent existence scans (CountEntities over the scene world's archetypes).
        internal int HighlightScanCount { get; private set; }

        internal void HighlightNewEntity(GlobalColliderSceneEntityInfo entityInfo, bool isAtDistance)
        {
            World world = entityInfo.EcsExecutor.World;
            Entity entityRef = entityInfo.ColliderSceneEntityInfo.EntityReference;

            // The HighlightComponent tracker entity is created once per scene World and only ever Reset()/reused (never
            // destroyed — see ResetHighlightComponent), so once it exists the answer is permanently yes. Cache that instead
            // of walking the world's archetypes with CountEntities every hover frame.
            if (highlightTrackerWorlds.TryGetValue(world, out _))
            {
                SetupHighlightComponentQuery(world, isAtDistance, entityRef);
                return;
            }

            HighlightScanCount++;
            int count = world.CountEntities(highlightQuery);

            if (count > 0)
                SetupHighlightComponentQuery(world, isAtDistance, entityRef);
            else
                world.Create(HighlightComponent.NewEntityHighlightComponent(isAtDistance, entityRef));

            highlightTrackerWorlds.Add(world, TRACKER_PRESENT);
        }

        [Pure]
        private static bool NewEntityWasHovered(
            bool candidateForHoverLeaveIsValid,
            in GlobalColliderSceneEntityInfo previousEntityInfo,
            in GlobalColliderSceneEntityInfo entityInfo
        ) =>
            candidateForHoverLeaveIsValid == false
            || previousEntityInfo.IsSameEntity(entityInfo) == false;

        [Pure]
        private static PointerEventType GetEnterEventType(InteractionType interactionType) =>
            interactionType == InteractionType.Cursor ? PointerEventType.PetHoverEnter : PointerEventType.PetProximityEnter;

        [Query]
        private void SetupHighlightComponent([Data] bool isAtDistance, [Data] Entity nextEntityRef, ref HighlightComponent highlightComponent)
        {
            highlightComponent.Setup(isAtDistance, nextEntityRef);
        }

        [Query]
        private void ResetHighlightComponent(ref HighlightComponent highlightComponent)
        {
            highlightComponent.Reset();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupPointerEvents(
            GlobalColliderSceneEntityInfo entityInfo,
            in PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities,
            in ProximityResultForSceneEntities proximityResultForSceneEntities,
            ref HoverFeedbackComponent hoverFeedbackComponent,
            PBPointerEvents pbPointerEvents,
            bool newEntityIsSelected,
            in SyntheticPointerInput synthetic,
            out bool isAtDistance
        )
        {
            isAtDistance = false;
            bool highlightEnabled = true;
            var anyInputInfo = sdkInputActionsMap.Values.GatherAnyInputInfo();

            if (synthetic.PressButton.HasValue || synthetic.ReleaseButton.HasValue)
                anyInputInfo = new InteractionInputUtils.AnyInputInfo(
                    anyInputInfo.AnyButtonWasPressedThisFrame || synthetic.PressButton.HasValue,
                    anyInputInfo.AnyButtonWasReleasedThisFrame || synthetic.ReleaseButton.HasValue,
                    anyInputInfo.AnyButtonIsPressed || synthetic.PressButton.HasValue);

            Vector2? screenPositionOverride = null;

            pbPointerEvents.AppendPointerEventResultsIntent.Initialize(raycastResultForSceneEntities.RaycastHit, raycastResultForSceneEntities.OriginRay);

            for (var i = 0; i < pbPointerEvents.PointerEvents!.Count; i++)
            {
                PBPointerEvents.Types.Entry pointerEvent = pbPointerEvents.PointerEvents[i]!;
                InteractionType interactionType = pointerEvent.InteractionType;
                PBPointerEvents.Types.Info info = pointerEvent.EventInfo!;
                bool isCursor = interactionType == InteractionType.Cursor;

                if (info is { HasShowFeedback: true, ShowFeedback: false }
                    or { HasShowHighlight: true, ShowHighlight: false })
                    highlightEnabled = false;

                info.PrepareDefaultValues();

                isAtDistance = isCursor
                    ? InteractionInputUtils.IsQualifiedByDistance(raycastResultForSceneEntities, info)
                    : InteractionInputUtils.IsQualifiedByDistance(proximityResultForSceneEntities, info);

                if (!isAtDistance) continue;

                if (newEntityIsSelected)
                    pbPointerEvents.AppendPointerEventResultsIntent.AppendPointerInputIfQualified(GetEnterEventType(interactionType), pointerEvent, i);

                if (!isCursor)
                    screenPositionOverride = GetColliderCenterScreenPosition(proximityResultForSceneEntities.Collider!);

                if (info.HasHoverText && !string.IsNullOrEmpty(info.HoverText))
                    HoverFeedbackUtils.TryAppendHoverFeedback(
                        sdkInputActionsMap,
                        pointerEvent,
                        ref hoverFeedbackComponent,
                        anyInputInfo.AnyButtonIsPressed);
            }

            hoverFeedbackComponent.ScreenPositionOverride = screenPositionOverride;

            if (highlightEnabled)
                HighlightNewEntity(entityInfo, isAtDistance);

            if (isAtDistance)
            {
                // Add all inputs that were pressed/unpressed this frame
                InteractionInputUtils.TryAppendButtonAction(sdkInputActionsMap, ref pbPointerEvents.AppendPointerEventResultsIntent);

                if (synthetic.PressButton.HasValue)
                    pbPointerEvents.AppendPointerEventResultsIntent.AddInputAction(synthetic.PressButton.Value, PointerEventType.PetDown);

                if (synthetic.ReleaseButton.HasValue)
                    pbPointerEvents.AppendPointerEventResultsIntent.AddInputAction(synthetic.ReleaseButton.Value, PointerEventType.PetUp);
            }
        }

        private Vector2 GetColliderCenterScreenPosition(Collider collider)
        {
            CameraComponent cameraComponent = playerCamera.GetCameraComponent(World);
            Camera camera = cameraComponent.Camera;

            return camera.WorldToScreenPoint(collider.bounds.center);
        }
    }
}
