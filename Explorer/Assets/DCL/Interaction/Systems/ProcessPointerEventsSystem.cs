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

            if (raycastResult.IsValidHit && canHover)
            {
                GlobalColliderEntityInfo entityInfo = raycastResult.EntityInfo.Value;

                InteractionInputUtils.AnyInputInfo anyInputInfo = sdkInputActionsMap.Values.GatherAnyInputInfo();

                // External world access should be always synchronized (Global World calls into Scene World)
                using (entityInfo.EcsExecutor.Sync.GetScope())
                {
                    World world = entityInfo.EcsExecutor.World;
                    EntityReference entityRef = entityInfo.ColliderEntityInfo.EntityReference;

                    // Entity should be alive and contain PBPointerEvents component to be qualified
                    if (entityRef.IsAlive(world) && world.TryGet(entityRef, out PBPointerEvents pbPointerEvents))
                    {
                        hoverStateComponent.LastHitCollider = raycastResult.UnityRaycastHit.collider;
                        hoverStateComponent.HasCollider = true;

                        bool newEntityWasHovered = !candidateForHoverLeaveIsValid
                                                   || (previousEntityInfo.EcsExecutor.World != world && previousEntityInfo.ColliderEntityInfo.EntityReference != entityRef);

                        // Signal to stop issuing hover leave event for the previous entity as it's equal to the current one
                        if (candidateForHoverLeaveIsValid && !newEntityWasHovered)
                            candidateForHoverLeaveIsValid = false;

                        pbPointerEvents.AppendPointerEventResultsIntent.Initialize(raycastResult.UnityRaycastHit, raycastResult.OriginRay);

                        bool isAtDistance = SetupPointerEvents(raycastResult, ref hoverFeedbackComponent, pbPointerEvents, anyInputInfo, newEntityWasHovered);

                        hoverStateComponent.IsAtDistance = isAtDistance;

                        int count = world.CountEntities(highlightQuery);

                        if (count > 0)
                        {
                            world.Query(highlightQuery, e =>
                            {
                                ref HighlightComponent highlightComponent = ref world.TryGetRef<HighlightComponent>(e, out bool exists);
                                if (!exists) return;
                                highlightComponent.IsEnabled = true;
                                highlightComponent.IsAtDistance = isAtDistance;
                                highlightComponent.NextEntity = entityRef;
                            });
                        }
                        else
                        {
                            world.Create(new HighlightComponent
                            {
                                NextEntity = entityRef,
                                CurrentEntity = entityRef,
                                IsEnabled = true,
                                IsAtDistance = isAtDistance,
                            });
                        }
                    }
                }
            }

            if (candidateForHoverLeaveIsValid)
            {
                hoverStateComponent.IsHoverOver = true;

                World world = previousEntityInfo.EcsExecutor.World;

                using (previousEntityInfo.EcsExecutor.Sync.GetScope())
                {
                    world.Query(highlightQuery, e =>
                    {
                        ref HighlightComponent highlightComponent = ref world.TryGetRef<HighlightComponent>(e, out bool exists);
                        if (!exists) return;
                        highlightComponent.IsEnabled = false;
                        highlightComponent.IsAtDistance = false;
                        highlightComponent.NextEntity = EntityReference.Null;
                    });
                }

                HoverFeedbackUtils.TryIssueLeaveHoverEventForPreviousEntity(in raycastResult, in previousEntityInfo);
            }
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

                if (!InteractionInputUtils.IsQualifiedByDistance(raycastResult, info)) continue;
                isAtDistance = true;

                // Check Input for validity
                InteractionInputUtils.TryAppendButtonLikeInput(sdkInputActionsMap, pointerEvent, i,
                    ref pbPointerEvents.AppendPointerEventResultsIntent, anyInputInfo);

                // Try Append Hover Input
                if (newEntityWasHovered)
                    InteractionInputUtils.TryAppendHoverInput(ref pbPointerEvents.AppendPointerEventResultsIntent, PointerEventType.PetHoverEnter, pointerEvent, i);

                // Try Append Hover Feedback
                HoverFeedbackUtils.TryAppendHoverFeedback(sdkInputActionsMap, pointerEvent,
                    ref hoverFeedbackComponent, anyInputInfo.AnyButtonIsPressed);
            }

            return isAtDistance;
        }
    }
}
