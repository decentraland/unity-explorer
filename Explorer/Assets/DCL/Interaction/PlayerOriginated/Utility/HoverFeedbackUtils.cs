using Arch.Core;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.Utility;
using System.Collections.Generic;

namespace DCL.Interaction.PlayerOriginated.Utility
{
    public static class HoverFeedbackUtils
    {
        public static void TryIssueLeaveHoverEventForPreviousEntity(in PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities, in GlobalColliderSceneEntityInfo previousSceneEntityInfo)
        {
            World world = previousSceneEntityInfo.EcsExecutor.World;

            // Entity died or PointerEvents component was removed, nothing to do
            if (!world.IsAlive(previousSceneEntityInfo.ColliderSceneEntityInfo.EntityReference) ||
                !world.TryGet(previousSceneEntityInfo.ColliderSceneEntityInfo.EntityReference, out PBPointerEvents pbPointerEvents))
                return;

            TryAppendHoverInput(ref pbPointerEvents, in raycastResultForSceneEntities, PointerEventType.PetHoverLeave);
        }

        private static void TryAppendHoverInput(ref PBPointerEvents pbPointerEvents, in PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities, PointerEventType type)
        {
            for (var i = 0; i < pbPointerEvents.PointerEvents.Count; i++)
            {
                PBPointerEvents.Types.Entry pointerEvent = pbPointerEvents.PointerEvents[i];
                PBPointerEvents.Types.Info info = pointerEvent.EventInfo;

                if (!InteractionInputUtils.IsQualifiedByDistance(raycastResultForSceneEntities, info)) continue;

                pbPointerEvents.AppendPointerEventResultsIntent.TryAppendHoverInput(type, pointerEvent, i);
            }
        }

        /// <summary>
        ///     Creating hover tooltips is completely separated from creating Event Results components
        ///     as it does not require information about raycast hit
        /// </summary>
        public static bool TryAppendHoverFeedback(IReadOnlyDictionary<InputAction, UnityEngine.InputSystem.InputAction> sdkInputActionsMap,
            in PBPointerEvents.Types.Entry pointerEventEntry, ref HoverFeedbackComponent hoverFeedbackComponent,
            bool anyButtonIsDown)
        {
            if (!pointerEventEntry.EventInfo.ShowFeedback)
                return false;

            // Down tooltips should be shown only if a key of interest is not down yet
            // Up tooltips should be shown only if a key of interest is down

            if (!sdkInputActionsMap.TryGetValue(pointerEventEntry.EventInfo.Button, out UnityEngine.InputSystem.InputAction unityInputAction))
                return false;

            if (pointerEventEntry.EventInfo.Button == InputAction.IaAny)
                switch (anyButtonIsDown)
                {
                    case false when pointerEventEntry.EventType != PointerEventType.PetDown:
                    case true when pointerEventEntry.EventType != PointerEventType.PetUp:
                        return false;
                }
            else
                switch (unityInputAction.IsPressed())
                {
                    case true when pointerEventEntry.EventType != PointerEventType.PetUp:
                    case false when pointerEventEntry.EventType != PointerEventType.PetDown:
                        return false;
                }

            hoverFeedbackComponent.Add(new HoverFeedbackComponent.Tooltip(pointerEventEntry.EventInfo.HoverText, unityInputAction));
            return true;
        }
    }
}
