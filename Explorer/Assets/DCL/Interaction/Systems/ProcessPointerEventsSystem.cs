using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Input;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.PlayerOriginated.Systems;
using DCL.Interaction.PlayerOriginated.Utility;
using DCL.Interaction.Raycast.Components;
using DCL.Interaction.Utility;
using ECS.Abstract;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using UnityEngine;
using static DCL.Interaction.PlayerOriginated.Components.ProximityResultForSceneEntities;
using WorldExtensions = DCL.CharacterCamera.WorldExtensions;

namespace DCL.Interaction.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PlayerOriginatedRaycastSystem))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class ProcessPointerEventsSystem : BaseUnityLoopSystem
    {
        private readonly IEntityCollidersGlobalCache entityCollidersGlobalCache;
        private readonly IEventSystem eventSystem;
        private readonly IReadOnlyDictionary<InputAction, UnityEngine.InputSystem.InputAction> sdkInputActionsMap;
        private readonly QueryDescription highlightQuery = new QueryDescription().WithAll<HighlightComponent>();

        private SingleInstanceEntity playerCamera;

        internal ProcessPointerEventsSystem(World world,
            IReadOnlyDictionary<InputAction, UnityEngine.InputSystem.InputAction> sdkInputActionsMap,
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
            ref HoverStateComponent hoverStateComponent)
        {
            // Process all PBPointerEvents components to see if any of them is qualified
            hoverFeedbackComponent.Clear();
            bool candidateForHoverLeaveIsValid = TryGetPreviousEntityInfo(in hoverStateComponent, out GlobalColliderSceneEntityInfo previousEntityInfo);
            hoverStateComponent.Clear();

            if (
                TryGetInteractableEntity(
                    in raycastResultForSceneEntities,
                    in proximityResultForSceneEntities,
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
            out GlobalColliderSceneEntityInfo entityInfo,
            out PBPointerEvents? pbPointerEvents,
            out Collider? collider
        )
        {
            // Check cursor type first
            if (TryGetInteractableEntityFromCursor(
                    in raycastResultForSceneEntities,
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
            out GlobalColliderSceneEntityInfo entityInfo,
            out PBPointerEvents? pbPointerEvents,
            out Collider? cursorCollider)
        {
            if (
                IsPointingOnEntity(in raycastResultForSceneEntities, out GlobalColliderSceneEntityInfo pointedEntityInfo)
                && pointedEntityInfo.TryGetPointerEvents(out PBPointerEvents? foundPointerEvents)
                && HasCursorEvent(in foundPointerEvents!))
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

            bool IsPointingOnEntity(in PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities, out GlobalColliderSceneEntityInfo entityInfo)
            {
                bool canHover = eventSystem.IsPointerOverGameObject() == false;
                entityInfo = raycastResultForSceneEntities.EntityInfo ?? default(GlobalColliderSceneEntityInfo);
                return raycastResultForSceneEntities.IsValidHit && canHover && raycastResultForSceneEntities.EntityInfo != null;
            }

            bool HasCursorEvent(in PBPointerEvents pointerEvents)
            {
                for (int i = 0; i < pointerEvents.PointerEvents.Count; i++)
                    if (pointerEvents.PointerEvents[i].InteractionType == InteractionType.Cursor)
                        return true;

                return false;
            }
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

        private void HighlightNewEntity(GlobalColliderSceneEntityInfo entityInfo, bool isAtDistance)
        {
            World world = entityInfo.EcsExecutor.World;
            Entity entityRef = entityInfo.ColliderSceneEntityInfo.EntityReference;
            int count = world.CountEntities(highlightQuery);

            if (count > 0)
                SetupHighlightComponentQuery(world, isAtDistance, entityRef);
            else
                world.Create(HighlightComponent.NewEntityHighlightComponent(isAtDistance, entityRef));
        }

        [Pure]
        private static bool NewEntityWasHovered(
            bool candidateForHoverLeaveIsValid,
            in GlobalColliderSceneEntityInfo previousEntityInfo,
            in GlobalColliderSceneEntityInfo entityInfo
        ) =>
            candidateForHoverLeaveIsValid == false
            || previousEntityInfo.IsSameEntity(entityInfo) == false;

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
            out bool isAtDistance
        )
        {
            isAtDistance = false;
            bool highlightEnabled = true;
            var anyInputInfo = sdkInputActionsMap.Values.GatherAnyInputInfo();
            Vector2? screenPositionOverride = null;

            pbPointerEvents.AppendPointerEventResultsIntent.Initialize(raycastResultForSceneEntities.RaycastHit, raycastResultForSceneEntities.OriginRay);

            for (var i = 0; i < pbPointerEvents.PointerEvents!.Count; i++)
            {
                PBPointerEvents.Types.Entry pointerEvent = pbPointerEvents.PointerEvents[i]!;
                InteractionType interactionType = pointerEvent.InteractionType;
                PBPointerEvents.Types.Info info = pointerEvent.EventInfo!;

                if (info is { HasShowFeedback: true, ShowFeedback: false }
                    or { HasShowHighlight: true, ShowHighlight: false })
                    highlightEnabled = false;

                info.PrepareDefaultValues();

                isAtDistance = interactionType == InteractionType.Cursor
                    ? InteractionInputUtils.IsQualifiedByDistance(raycastResultForSceneEntities, info)
                    : InteractionInputUtils.IsQualifiedByDistance(proximityResultForSceneEntities, info);

                if (!isAtDistance) continue;

                // Try Append Hover Input
                if (newEntityIsSelected)
                {
                    PointerEventType eventType = interactionType == InteractionType.Cursor
                        ? PointerEventType.PetHoverEnter
                        : PointerEventType.PetProximityEnter;

                    pbPointerEvents.AppendPointerEventResultsIntent.TryAppendEnterOrLeaveInput(eventType, pointerEvent, i);
                }

                // Try Append Hover Feedback
                if (interactionType == InteractionType.Proximity)
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

                // Add all inputs that were pressed/unpressed this frame
                InteractionInputUtils.TryAppendButtonAction(sdkInputActionsMap, ref pbPointerEvents.AppendPointerEventResultsIntent);
        }

        private Vector2 GetColliderCenterScreenPosition(Collider collider)
        {
            CameraComponent cameraComponent = playerCamera.GetCameraComponent(World);
            Camera camera = cameraComponent.Camera;

            return camera.WorldToScreenPoint(collider.bounds.center);
        }
    }
}
