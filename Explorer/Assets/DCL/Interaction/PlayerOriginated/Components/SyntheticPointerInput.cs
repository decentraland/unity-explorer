using Arch.Core;
using DCL.ECSComponents;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Interaction.PlayerOriginated.Components
{
    /// <summary>
    ///     Single-frame instructions for the player-origin pointer pipeline, posted onto the player interaction
    ///     entity by an automation driver: aim the reticle ray at a world point and/or press or release a pointer
    ///     button as if the player did. Posting is last-write-wins.
    /// </summary>
    public struct SyntheticPointerInput
    {
        /// <summary>
        ///     Squared minimum distance from the camera origin to <see cref="AimPoint" />; any closer and no usable
        ///     aim ray can be built, so drivers must fail such a request upfront before posting it.
        /// </summary>
        public const float MIN_AIM_DISTANCE_SQR = 0.0001f;

        /// <summary>World point the reticle ray is forced to pass through this frame; null keeps the cursor ray.</summary>
        public Vector3? AimPoint;

        /// <summary>Button reported as pressed this frame.</summary>
        public InputAction? PressButton;

        /// <summary>Button reported as released this frame.</summary>
        public InputAction? ReleaseButton;

        /// <summary>The world that owns <see cref="TargetEntity" />; an Arch entity id is unique only within its world.</summary>
        public World? TargetWorld;

        /// <summary>The only entity that may receive this post's button edge. Null lets the edge go to whatever the ray selected.</summary>
        public Entity? TargetEntity;

        public readonly bool HasTargetEntity => TargetEntity.HasValue;

        /// <summary><see cref="UnityEngine.Time.frameCount" /> at the moment of posting; every poster must stamp it.</summary>
        public int PostedAtFrame;

        /// <summary>The instructions are valid only while this holds; readers treat a stale post as absent.</summary>
        public bool IsPostedThisFrame => PostedAtFrame == UnityEngine.Time.frameCount;

        /// <summary>
        ///     The button edges this post delivers to a receiver this frame. A null <paramref name="receiver" /> is the
        ///     scene root. An edge of an action the player really pressed or released this frame is withheld, so that
        ///     no receiver observes the same edge twice.
        /// </summary>
        public readonly void DeliverableEdgesFor(World? receiverWorld, Entity? receiver,
            IReadOnlyDictionary<InputAction, UnityEngine.InputSystem.InputAction> sdkInputActionsMap,
            out InputAction? press, out InputAction? release)
        {
            press = null;
            release = null;

            if (!IsPostedThisFrame || !MayDeliverEdgeTo(receiverWorld, receiver))
                return;

            if (PressButton is { } pressed && !WasReallyPressedThisFrame(sdkInputActionsMap, pressed))
                press = pressed;

            if (ReleaseButton is { } released && !WasReallyReleasedThisFrame(sdkInputActionsMap, released))
                release = released;
        }

        /// <summary>True when <paramref name="receiver" /> (null is the scene root) may receive this post's button edge.</summary>
        public readonly bool MayDeliverEdgeTo(World? receiverWorld, Entity? receiver) =>
            TargetEntity is not { } target || (receiver is { } entity && ReferenceEquals(receiverWorld, TargetWorld) && entity == target);

        private static bool WasReallyPressedThisFrame(IReadOnlyDictionary<InputAction, UnityEngine.InputSystem.InputAction> sdkInputActionsMap, InputAction action) =>
            sdkInputActionsMap.TryGetValue(action, out UnityEngine.InputSystem.InputAction unityAction) && unityAction.WasPressedThisFrame();

        private static bool WasReallyReleasedThisFrame(IReadOnlyDictionary<InputAction, UnityEngine.InputSystem.InputAction> sdkInputActionsMap, InputAction action) =>
            sdkInputActionsMap.TryGetValue(action, out UnityEngine.InputSystem.InputAction unityAction) && unityAction.WasReleasedThisFrame();
    }
}
