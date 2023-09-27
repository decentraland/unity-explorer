using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using InputAction = UnityEngine.InputSystem.InputAction;

namespace DCL.Interaction.PlayerOriginated.Utility
{
    public static class InteractionInputUtils
    {
        internal static AnyInputInfo GatherAnyInputInfo(this IEnumerable<InputAction> eligibleInputActions)
        {
            var anyButtonWasPressedThisFrame = false;
            var anyButtonWasReleasedThisFrame = false;
            var anyButtonIsPressed = false;

            foreach (InputAction inputAction in eligibleInputActions)
            {
                // Break the loop as soon as we resolve all press state
                // Note: & is used instead of && to ensure all input actions are evaluated
                if ((anyButtonWasPressedThisFrame |= inputAction.WasPressedThisFrame())
                    & (anyButtonWasReleasedThisFrame |= inputAction.WasReleasedThisFrame())
                    & (anyButtonIsPressed |= inputAction.IsPressed()))
                    break;
            }

            return new AnyInputInfo(anyButtonWasPressedThisFrame, anyButtonWasReleasedThisFrame, anyButtonIsPressed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsQualifiedByDistance(in PlayerOriginRaycastResult raycastResult, PBPointerEvents.Types.Info info) =>
            !(raycastResult.UnityRaycastHit.distance > info.MaxDistance);

        /// <summary>
        ///     Adds hover input if the entry is qualified for listening to it
        ///     <para>
        ///         Entry is qualified if the expected Button is "Pointer" or "Any", and event type is corresponding "HoverEnter"/"HoverExit"
        ///     </para>
        /// </summary>
        internal static void TryAppendHoverInput(ref AppendPointerEventResultsIntent resultsIntent, PointerEventType? hoverEventType, in PBPointerEvents.Types.Entry entry, int entryIndex)
        {
            if (!hoverEventType.HasValue) return;

            if (entry.EventType == hoverEventType.Value
                && (entry.EventInfo.Button == ECSComponents.InputAction.IaPointer || entry.EventInfo.Button == ECSComponents.InputAction.IaAny))
                resultsIntent.ValidIndices.Add((byte)entryIndex);
        }

        /// <summary>
        ///     Handler Pointer Up and Pointer Down, check the corresponding input action if it was upped or downed this frame
        /// </summary>
        internal static void TryAppendButtonLikeInput(IReadOnlyDictionary<ECSComponents.InputAction, InputAction> sdkInputActionsMap,
            in PBPointerEvents.Types.Entry entry, int entryIndex,
            ref AppendPointerEventResultsIntent resultsIntent, in AnyInputInfo anyInputInfo)
        {
            switch (entry.EventType)
            {
                case PointerEventType.PetDown:
                    if (entry.EventInfo.Button == ECSComponents.InputAction.IaAny)
                    {
                        if (!anyInputInfo.AnyButtonWasPressedThisFrame)
                            return;

                        break;
                    }

                    if (!sdkInputActionsMap.TryGetValue(entry.EventInfo.Button, out InputAction unityInputAction) || !unityInputAction.WasPressedThisFrame())
                        return;

                    break;

                case PointerEventType.PetUp:
                    if (entry.EventInfo.Button == ECSComponents.InputAction.IaAny)
                    {
                        if (!anyInputInfo.AnyButtonWasReleasedThisFrame)
                            return;

                        break;
                    }

                    if (!sdkInputActionsMap.TryGetValue(entry.EventInfo.Button, out unityInputAction) || !unityInputAction.WasReleasedThisFrame())
                        return;

                    break;

                default:
                    return;
            }

            resultsIntent.ValidIndices.Add((byte)entryIndex);
        }

        public readonly struct AnyInputInfo
        {
            public readonly bool AnyButtonWasPressedThisFrame;
            public readonly bool AnyButtonWasReleasedThisFrame;
            public readonly bool AnyButtonIsPressed;

            public AnyInputInfo(bool anyButtonWasPressedThisFrame, bool anyButtonWasReleasedThisFrame, bool anyButtonIsPressed)
            {
                AnyButtonWasPressedThisFrame = anyButtonWasPressedThisFrame;
                AnyButtonWasReleasedThisFrame = anyButtonWasReleasedThisFrame;
                AnyButtonIsPressed = anyButtonIsPressed;
            }
        }
    }
}
