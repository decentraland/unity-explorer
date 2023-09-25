using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.PlayerOriginated.Utility;
using DCL.Interaction.Utility;
using ECS.Abstract;
using System.Collections.Generic;

namespace DCL.Interaction.PlayerOriginated.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PlayerOriginatedRaycastSystem))]
    public partial class ProcessPointerEventsSystem : BaseUnityLoopSystem
    {
        private readonly IEntityCollidersGlobalCache entityCollidersGlobalCache;
        private readonly IReadOnlyDictionary<InputAction, UnityEngine.InputSystem.InputAction> sdkInputActionsMap;

        internal ProcessPointerEventsSystem(World world,
            IReadOnlyDictionary<InputAction, UnityEngine.InputSystem.InputAction> sdkInputActionsMap,
            IEntityCollidersGlobalCache entityCollidersGlobalCache) : base(world)
        {
            this.sdkInputActionsMap = sdkInputActionsMap;
            this.entityCollidersGlobalCache = entityCollidersGlobalCache;
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
        private void ProcessRaycastResult(ref PlayerOriginRaycastResult raycastResult, ref HoverFeedbackComponent hoverFeedbackComponent,
            ref HoverStateComponent hoverStateComponent)
        {
            // Process all PBPointerEvents components to see if any of them is qualified
            hoverFeedbackComponent.Tooltips.Clear();

            bool candidateForHoverLeaveIsValid = TryGetPreviousEntityInfo(in hoverStateComponent, out GlobalColliderEntityInfo previousEntityInfo);
            hoverStateComponent.LastHitCollider = null;

            if (raycastResult.IsValidHit)
            {
                GlobalColliderEntityInfo entityInfo = raycastResult.EntityInfo.Value;

                InteractionInputUtils.AnyInputInfo anyInputInfo = sdkInputActionsMap.Values.GatherAnyInputInfo();

                // Entity should be alive and contain PBPointerEvents component to be qualified
                using (entityInfo.EcsExecutor.Sync.GetScope())
                {
                    // External world access should be always synchronized
                    World world = entityInfo.EcsExecutor.World;
                    EntityReference entityRef = entityInfo.ColliderEntityInfo.EntityReference;

                    if (entityRef.IsAlive(world) && world.TryGet(entityRef, out PBPointerEvents pbPointerEvents))
                    {
                        hoverStateComponent.LastHitCollider = raycastResult.UnityRaycastHit.collider;

                        bool newEntityWasHovered = !candidateForHoverLeaveIsValid
                                                   || (previousEntityInfo.EcsExecutor.World != world && previousEntityInfo.ColliderEntityInfo.EntityReference != entityRef);

                        // Signal to stop issuing hover leave event for the previous entity as it's equal to the current one
                        if (candidateForHoverLeaveIsValid && !newEntityWasHovered)
                            candidateForHoverLeaveIsValid = false;

                        pbPointerEvents.AppendPointerEventResultsIntent.Initialize(raycastResult.UnityRaycastHit, raycastResult.OriginRay);

                        for (var i = 0; i < pbPointerEvents.PointerEvents.Count; i++)
                        {
                            PBPointerEvents.Types.Entry pointerEvent = pbPointerEvents.PointerEvents[i];
                            PBPointerEvents.Types.Info info = pointerEvent.EventInfo;

                            if (!InteractionInputUtils.IsQualifiedByDistance(raycastResult, info)) continue;

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
                    }
                }
            }

            if (candidateForHoverLeaveIsValid)
                HoverFeedbackUtils.TryIssueLeaveHoverEventForPreviousEntity(in raycastResult, in previousEntityInfo);
        }
    }
}
