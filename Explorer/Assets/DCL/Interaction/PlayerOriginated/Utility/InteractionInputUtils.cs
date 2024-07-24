using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using InputAction = UnityEngine.InputSystem.InputAction;

namespace DCL.Interaction.PlayerOriginated.Utility
{
    public static class InteractionInputUtils
    {
        public static AnyInputInfo GatherAnyInputInfo(this IEnumerable<InputAction> eligibleInputActions)
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
        public static bool IsQualifiedByDistance(in PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities, PBPointerEvents.Types.Info info) =>
            !(raycastResultForSceneEntities.GetDistance() > info.MaxDistance);

        /// <summary>
        ///     Handler Pointer Up and Pointer Down, check the corresponding input action if it was upped or downed this frame
        /// </summary>
        public static void TryAppendButtonLikeInput(IReadOnlyDictionary<ECSComponents.InputAction, InputAction> sdkInputActionsMap,
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

        public static void TryAppendButtonAction(IReadOnlyDictionary<DCL.ECSComponents.InputAction, InputAction> sdkInputActionsMap, ref AppendPointerEventResultsIntent resultsIntent)
        {
            foreach (var input in sdkInputActionsMap)

                // Add all inputs that were pressed/unpressed this frame
                TryAppendButtonAction(input.Value!, input.Key, ref resultsIntent);
        }

        /// <summary>
        ///     Handler Pointer Up and Pointer Down, check the corresponding input action if it was upped or downed this frame
        /// </summary>
        public static void TryAppendButtonAction(InputAction inputAction, DCL.ECSComponents.InputAction ecsInputAction,
            ref AppendPointerEventResultsIntent resultsIntent)
        {
            if (inputAction.WasPressedThisFrame())
            {
                resultsIntent.ValidInputActions.Add(ecsInputAction, PointerEventType.PetDown);
                return;
            }

            if (inputAction.WasReleasedThisFrame())
            {
                resultsIntent.ValidInputActions.Add(ecsInputAction, PointerEventType.PetUp);
                return;
            }
        }

        public static void PrepareDefaultValues(this PBPointerEvents.Types.Info info)
        {
            if (!info.HasButton)
                info.Button = ECSComponents.InputAction.IaAny;

            if (!info.HasMaxDistance)
                info.MaxDistance = 10f;

            if (!info.HasShowFeedback)
                info.ShowFeedback = true;

            if (!info.HasHoverText)
                info.HoverText = "Interact";
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
