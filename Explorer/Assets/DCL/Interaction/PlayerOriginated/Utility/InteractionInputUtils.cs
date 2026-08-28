using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using InputAction = UnityEngine.InputSystem.InputAction;

namespace DCL.Interaction.PlayerOriginated.Utility
{
    public static class InteractionInputUtils
    {
        /// <summary>Player-distance threshold when neither max_distance nor max_camera_distance is set</summary>
        public const float DEFAULT_MAX_DISTANCE = 10f;

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
        public static bool IsQualifiedByDistance(
            in PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities,
            PBPointerEvents.Types.Info info
        )
        {
            float? maxPlayerDistance = ResolveMaxPlayerDistance(info);
            float? maxCameraDistance = info.HasMaxCameraDistance ? info.MaxCameraDistance : null;

            return (maxPlayerDistance, maxCameraDistance) switch
            {
                (null, null) => raycastResultForSceneEntities.DistanceToPlayer <= DEFAULT_MAX_DISTANCE,
                ({ } player, null) => raycastResultForSceneEntities.DistanceToPlayer <= player,
                (null, { } cam) => raycastResultForSceneEntities.GetDistance() <= cam,
                ({ } player, { } cam) => raycastResultForSceneEntities.DistanceToPlayer <= player
                                         || raycastResultForSceneEntities.GetDistance() <= cam,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsQualifiedByDistance(
            in ProximityResultForSceneEntities proximityResultForSceneEntities,
            PBPointerEvents.Types.Info info
        ) =>
            proximityResultForSceneEntities.DistanceToPlayer <= (ResolveMaxPlayerDistance(info) ?? DEFAULT_MAX_DISTANCE);

        /// <summary>
        ///     The player-distance threshold of an entry, or null when it sets neither field.
        ///     <c>max_distance</c> is the player-distance threshold; <c>max_player_distance</c> is its deprecated alias (larger wins).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? ResolveMaxPlayerDistance(PBPointerEvents.Types.Info info) =>
            (info.HasMaxDistance, info.HasMaxPlayerDistance) switch
            {
                (true, true) => Mathf.Max(info.MaxDistance, info.MaxPlayerDistance),
                (true, false) => info.MaxDistance,
                (false, true) => info.MaxPlayerDistance,
                _ => null,
            };

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

            resultsIntent.AddValidIndex((byte)entryIndex);
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
                resultsIntent.AddInputAction(ecsInputAction, PointerEventType.PetDown);
                return;
            }

            if (inputAction.WasReleasedThisFrame())
                resultsIntent.AddInputAction(ecsInputAction, PointerEventType.PetUp);
        }

        public static void PrepareDefaultValues(this PBPointerEvents.Types.Info info)
        {
            if (!info.HasButton)
                info.Button = ECSComponents.InputAction.IaAny;

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
