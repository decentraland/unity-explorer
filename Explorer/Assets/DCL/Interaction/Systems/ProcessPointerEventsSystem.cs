using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
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
using System.Runtime.CompilerServices;

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

        internal ProcessPointerEventsSystem(World world,
            IReadOnlyDictionary<InputAction, UnityEngine.InputSystem.InputAction> sdkInputActionsMap,
            IEntityCollidersGlobalCache entityCollidersGlobalCache,
            IEventSystem eventSystem) : base(world)
        {
            this.sdkInputActionsMap = sdkInputActionsMap;
            this.entityCollidersGlobalCache = entityCollidersGlobalCache;

            this.eventSystem = eventSystem;
        }

        protected override void Update(float t)
        {
            ProcessRaycastResultQuery(World);
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

        [Query]
        private void ProcessRaycastResult(ref PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities, ref HoverFeedbackComponent hoverFeedbackComponent, ref HoverStateComponent hoverStateComponent)
        {
            // Process all PBPointerEvents components to see if any of them is qualified
            hoverFeedbackComponent.Clear();

            bool candidateForHoverLeaveIsValid = TryGetPreviousEntityInfo(in hoverStateComponent, out GlobalColliderSceneEntityInfo previousEntityInfo);
            hoverStateComponent.Clear();

            bool canHover = !eventSystem.IsPointerOverGameObject();
            GlobalColliderSceneEntityInfo? entityInfo = raycastResultForSceneEntities.GetEntityInfo();

            if (raycastResultForSceneEntities.IsValidHit && canHover && entityInfo != null)
            {
                ColliderSceneEntityInfo colliderSceneInfo = entityInfo.Value.ColliderSceneEntityInfo;

                InteractionInputUtils.AnyInputInfo anyInputInfo = sdkInputActionsMap.Values.GatherAnyInputInfo();

                World world = entityInfo.Value.EcsExecutor.World;
                EntityReference entityRef = colliderSceneInfo.EntityReference;

                // Entity should be alive and contain PBPointerEvents component to be qualified for highlighting
                if (entityRef.IsAlive(world) && world.TryGet(entityRef, out PBPointerEvents pbPointerEvents))
                {
                    hoverStateComponent.AssignCollider(raycastResultForSceneEntities.Collider);

                    bool newEntityWasHovered = NewEntityWasHovered(candidateForHoverLeaveIsValid, previousEntityInfo, entityInfo.Value);

                    // Signal to stop issuing hover leave event for the previous entity as it's equal to the current one
                    if (candidateForHoverLeaveIsValid && newEntityWasHovered == false)
                        candidateForHoverLeaveIsValid = false;

                    pbPointerEvents!.AppendPointerEventResultsIntent.Initialize(raycastResultForSceneEntities.GetRaycastHit(), raycastResultForSceneEntities.GetOriginRay());

                    bool isAtDistance = SetupPointerEvents(raycastResultForSceneEntities, ref hoverFeedbackComponent, pbPointerEvents, anyInputInfo, newEntityWasHovered);

                    hoverStateComponent.IsAtDistance = isAtDistance;

                    int count = world.CountEntities(highlightQuery);

                    if (count > 0)
                        SetupHighlightComponentQuery(world, isAtDistance, entityRef);
                    else
                        world.Create(
                            HighlightComponent.NewEntityHighlightComponent(isAtDistance, entityRef)
                        );
                }
            }

            if (candidateForHoverLeaveIsValid)
                ResetPreviousEntity(in raycastResultForSceneEntities, in previousEntityInfo);
        }

        private void ResetPreviousEntity(in PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities, in GlobalColliderSceneEntityInfo previousEntityInfo)
        {
            ResetHighlightComponentQuery(previousEntityInfo.EcsExecutor.World);
            HoverFeedbackUtils.TryIssueLeaveHoverEventForPreviousEntity(in raycastResultForSceneEntities, in previousEntityInfo);
        }

        private bool NewEntityWasHovered(
            bool candidateForHoverLeaveIsValid,
            GlobalColliderSceneEntityInfo previousEntityInfo,
            GlobalColliderSceneEntityInfo entityInfo
        ) =>
            candidateForHoverLeaveIsValid == false
            || (
                previousEntityInfo.EcsExecutor.World != entityInfo.EcsExecutor.World
                && previousEntityInfo.ColliderSceneEntityInfo.EntityReference != entityInfo.ColliderSceneEntityInfo.EntityReference
            );

        [Query]
        private void SetupHighlightComponent([Data] bool isAtDistance, [Data] EntityReference nextEntity, ref HighlightComponent highlightComponent)
        {
            highlightComponent.Setup(isAtDistance, nextEntity);
        }

        [Query]
        private void ResetHighlightComponent(ref HighlightComponent highlightComponent)
        {
            highlightComponent.Reset();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool SetupPointerEvents(PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities,
            ref HoverFeedbackComponent hoverFeedbackComponent, PBPointerEvents pbPointerEvents, InteractionInputUtils.AnyInputInfo anyInputInfo,
            bool newEntityWasHovered)
        {
            var isAtDistance = false;

            for (var i = 0; i < pbPointerEvents.PointerEvents.Count; i++)
            {
                PBPointerEvents.Types.Entry pointerEvent = pbPointerEvents.PointerEvents[i];
                PBPointerEvents.Types.Info info = pointerEvent.EventInfo;

                info.PrepareDefaultValues();

                isAtDistance = InteractionInputUtils.IsQualifiedByDistance(raycastResultForSceneEntities, info);
                if (!isAtDistance) continue;

                // Try Append Hover Input
                if (newEntityWasHovered)
                    InteractionInputUtils.TryAppendHoverInput(ref pbPointerEvents.AppendPointerEventResultsIntent, PointerEventType.PetHoverEnter, pointerEvent, i);

                // Try Append Hover Feedback
                HoverFeedbackUtils.TryAppendHoverFeedback(sdkInputActionsMap, pointerEvent,
                    ref hoverFeedbackComponent, anyInputInfo.AnyButtonIsPressed);
            }

            if (!isAtDistance) return false;

            foreach (var input in sdkInputActionsMap)
            {
                // Add all inputs that were pressed/unpressed this frame
                InteractionInputUtils.TryAppendButtonAction(input.Value, input.Key, ref pbPointerEvents.AppendPointerEventResultsIntent);
            }

            return isAtDistance;
        }
    }
}
