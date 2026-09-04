using Arch.Core;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.Utility;
using System.Collections.Generic;

namespace DCL.Interaction.PlayerOriginated.Utility
{
    public static class HoverFeedbackUtils
    {
        /// <summary>
        ///     Issues the hover leave for the entity hovered until now. <paramref name="previousHoverEnterIssued" />
        ///     is whether that hover produced an enter: the leave completes an enter that was actually issued, so it
        ///     must never be re-qualified against the ray of the frame the hover ended on — that ray points somewhere
        ///     else, and a target with a tight maxDistance would keep a hover the scene can never see end (the
        ///     proximity leave path is unconditional for the same reason).
        /// </summary>
        public static void TryIssueLeaveHoverEventForPreviousEntity(in GlobalColliderSceneEntityInfo previousSceneEntityInfo, bool previousHoverEnterIssued)
        {
            if (!previousHoverEnterIssued)
                return;

            World world = previousSceneEntityInfo.EcsExecutor.World;

            // Entity died or PointerEvents component was removed, nothing to do
            if (!world.IsAlive(previousSceneEntityInfo.ColliderSceneEntityInfo.EntityReference) ||
                !world.TryGet(previousSceneEntityInfo.ColliderSceneEntityInfo.EntityReference, out PBPointerEvents? pbPointerEvents))
                return;

            AppendHoverInput(ref pbPointerEvents!, PointerEventType.PetHoverLeave);
        }

        private static void AppendHoverInput(ref PBPointerEvents pbPointerEvents, PointerEventType type)
        {
            for (var i = 0; i < pbPointerEvents.PointerEvents.Count; i++)
                pbPointerEvents.AppendPointerEventResultsIntent.AppendPointerInputIfQualified(type, pbPointerEvents.PointerEvents[i], i);
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
