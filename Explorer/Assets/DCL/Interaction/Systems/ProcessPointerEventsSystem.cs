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
using SceneRunner.Scene;
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

        private bool TryGetPreviousEntityInfo(in HoverStateComponent stateComponent, out GlobalColliderEntityInfo globalColliderEntityInfo)
        {
            if (!stateComponent.LastHitCollider) // collider was destroyed, nothing to do
            {
                globalColliderEntityInfo = default(GlobalColliderEntityInfo);
                return false;
            }

            return entityCollidersGlobalCache.TryGetEntity(stateComponent.LastHitCollider, out globalColliderEntityInfo); // scene was destroyed, collider was returned to the pool, nothing to do
        }

        [Query]
        private void ProcessRaycastResult(ref PlayerOriginRaycastResult raycastResult, ref HoverFeedbackComponent hoverFeedbackComponent, ref HoverStateComponent hoverStateComponent)
        {
            // Process all PBPointerEvents components to see if any of them is qualified
            hoverFeedbackComponent.Tooltips.Clear();

            bool candidateForHoverLeaveIsValid = TryGetPreviousEntityInfo(in hoverStateComponent, out GlobalColliderEntityInfo previousEntityInfo);
            hoverStateComponent.LastHitCollider = null;
            hoverStateComponent.HasCollider = false;
            hoverStateComponent.IsHoverOver = false;
            hoverStateComponent.IsAtDistance = false;

            bool canHover = !eventSystem.IsPointerOverGameObject();
            GlobalColliderEntityInfo? entityInfo = raycastResult.GetEntityInfo();

            if (raycastResult.IsValidHit && canHover && entityInfo != null)
            {
                SceneEcsExecutor ecsExecutor = entityInfo.Value.EcsExecutor;
                ColliderEntityInfo colliderInfo = entityInfo.Value.ColliderEntityInfo;

                InteractionInputUtils.AnyInputInfo anyInputInfo = sdkInputActionsMap.Values.GatherAnyInputInfo();

                World world = ecsExecutor.World;
                EntityReference entityRef = colliderInfo.EntityReference;
                if (entityRef.IsAlive(world))
                {
                    // Entity should contain PBPointerEvents component to be qualified for highlighting
                    if (world.TryGet(entityRef, out PBPointerEvents pbPointerEvents))
                    {
                        hoverStateComponent.LastHitCollider = raycastResult.GetCollider();
                        hoverStateComponent.HasCollider = true;

                        bool newEntityWasHovered = !candidateForHoverLeaveIsValid
                                                   || (previousEntityInfo.EcsExecutor.World != world && previousEntityInfo.ColliderEntityInfo.EntityReference != entityRef);

                        // Signal to stop issuing hover leave event for the previous entity as it's equal to the current one
                        if (candidateForHoverLeaveIsValid && !newEntityWasHovered)
                            candidateForHoverLeaveIsValid = false;

                        pbPointerEvents!.AppendPointerEventResultsIntent.Initialize(raycastResult.GetRaycastHit(), raycastResult.GetOriginRay());

                        bool isAtDistance = SetupPointerEvents(raycastResult, ref hoverFeedbackComponent, pbPointerEvents, anyInputInfo, newEntityWasHovered);

                        hoverStateComponent.IsAtDistance = isAtDistance;

                        int count = world.CountEntities(highlightQuery);

                        if (count > 0) { SetupHighlightComponentQuery(world, isAtDistance, entityRef); }
                        else
                        {
                            world.Create(
                                new HighlightComponent(
                                    true,
                                    isAtDistance,
                                    entityRef,
                                    entityRef
                                )
                            );
                        }
                    }
                    //If the entity doesnt have a PBPointerEvent means we wont check distance and just send any input we receive to it.
                    else
                    {
                        foreach (var input in sdkInputActionsMap)
                        {
                            // Add all inputs that were pressed/unpressed this frame
                            InteractionInputUtils.TryAppendButtonAction(input.Value, input.Key, raycastResult.ValidInputActions);
                        }
                    }
                }
            }

            if (candidateForHoverLeaveIsValid)
            {
                hoverStateComponent.IsHoverOver = true;

                World world = previousEntityInfo.EcsExecutor.World;

                ResetHighlightComponentQuery(world);

                HoverFeedbackUtils.TryIssueLeaveHoverEventForPreviousEntity(in raycastResult, in previousEntityInfo);
            }
        }

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
        private bool SetupPointerEvents(PlayerOriginRaycastResult raycastResult,
            ref HoverFeedbackComponent hoverFeedbackComponent, PBPointerEvents pbPointerEvents, InteractionInputUtils.AnyInputInfo anyInputInfo,
            bool newEntityWasHovered)
        {
            var isAtDistance = false;

            for (var i = 0; i < pbPointerEvents.PointerEvents.Count; i++)
            {
                PBPointerEvents.Types.Entry pointerEvent = pbPointerEvents.PointerEvents[i];
                PBPointerEvents.Types.Info info = pointerEvent.EventInfo;

                info.PrepareDefaultValues();

                isAtDistance = InteractionInputUtils.IsQualifiedByDistance(raycastResult, info);
                if (!isAtDistance) continue;

                // Try Append Hover Input
                if (newEntityWasHovered)
                    InteractionInputUtils.TryAppendHoverInput(ref pbPointerEvents.AppendPointerEventResultsIntent, PointerEventType.PetHoverEnter, pointerEvent, i);

                // Try Append Hover Feedback
                HoverFeedbackUtils.TryAppendHoverFeedback(sdkInputActionsMap, pointerEvent,
                    ref hoverFeedbackComponent, anyInputInfo.AnyButtonIsPressed);

            }

            if (!isAtDistance) return false; //This only works if elements have PBPointerEvents to filter distances, otherwise, we need to send the input event anyway (not done yet)

            foreach (var input in sdkInputActionsMap)
            {
                // Add all inputs that were pressed/unpressed this frame
                InteractionInputUtils.TryAppendButtonAction(input.Value, input.Key, pbPointerEvents.AppendPointerEventResultsIntent.ValidInputActions);
            }

            return isAtDistance;
        }
    }
}
