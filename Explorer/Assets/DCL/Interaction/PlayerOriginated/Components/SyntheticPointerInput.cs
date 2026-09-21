using Arch.Core;
using DCL.ECSComponents;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Interaction.PlayerOriginated.Components
{
    /// <summary>
    ///     Single-frame instructions for the player-origin pointer pipeline, posted onto the player interaction
    ///     entity by an automation driver: aim the reticle ray at a world point and/or press or release a pointer
    ///     button as if the player did. PlayerOriginatedRaycastSystem reads the aim and echoes the point it consumed
    ///     in <see cref="PlayerOriginRaycastResultForSceneEntities.SyntheticAimPoint" /> (null on frames it guards
    ///     away); ProcessPointerEventsSystem and PrepareGlobalInputEventsSystem read the buttons through
    ///     <see cref="DeliverableEdgesFor" />, and the former clears the component. Both honour a post only during
    ///     the frame recorded in <see cref="PostedAtFrame" />, so a post that outlived the frame is discarded unread
    ///     and nobody has to sweep up instructions abandoned mid-pause. Posting is last-write-wins.
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

        /// <summary>
        ///     The scene world the target entity belongs to; paired with <see cref="TargetEntity" />, since an Arch
        ///     entity id is only unique within its own world and every loaded scene shares one physics scene.
        /// </summary>
        public World? TargetWorld;

        /// <summary>
        ///     The only entity allowed to consume this post's button edge. Null accepts whatever the pipeline's own
        ///     ray selected, the contract of an aim that names no entity. A driver that named an entity has been
        ///     promised it: the edge must not fall through to a nearer collider the driver was told blocked its aim,
        ///     nor to a proximity entity it never aimed at.
        /// </summary>
        public Entity? TargetEntity;

        /// <summary>True when the post names one entity as the only permitted receiver of its button edge.</summary>
        public readonly bool HasTargetEntity => TargetEntity.HasValue;

        /// <summary><see cref="UnityEngine.Time.frameCount" /> at the moment of posting; every poster must stamp it.</summary>
        public int PostedAtFrame;

        /// <summary>The instructions are valid only while this holds; readers treat a stale post as absent.</summary>
        public bool IsPostedThisFrame => PostedAtFrame == UnityEngine.Time.frameCount;

        /// <summary>
        ///     The button edges this post delivers to one receiver this frame: a scene entity, or the scene root
        ///     when <paramref name="receiver" /> is null. This is the single rule every consumer applies. A targeted
        ///     post delivers to its own entity alone (matched by world reference and entity), so the edge never lands
        ///     on the root broadcast or on another entity the ray happened to select. An edge of an action the player
        ///     really pressed or released this same frame is withheld from every receiver: the real-input path has
        ///     already added it, and no receiver may observe the same edge twice.
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

        /// <summary>
        ///     Whether a receiver (a scene entity, or the scene root when <paramref name="receiver" /> is null) is a
        ///     permitted consumer of this post's button edge. An untargeted post permits every receiver; a targeted
        ///     one permits exactly its own entity.
        /// </summary>
        public readonly bool MayDeliverEdgeTo(World? receiverWorld, Entity? receiver) =>
            TargetEntity is not { } target || (receiver is { } entity && ReferenceEquals(receiverWorld, TargetWorld) && entity == target);

        private static bool WasReallyPressedThisFrame(IReadOnlyDictionary<InputAction, UnityEngine.InputSystem.InputAction> sdkInputActionsMap, InputAction action) =>
            sdkInputActionsMap.TryGetValue(action, out UnityEngine.InputSystem.InputAction unityAction) && unityAction.WasPressedThisFrame();

        private static bool WasReallyReleasedThisFrame(IReadOnlyDictionary<InputAction, UnityEngine.InputSystem.InputAction> sdkInputActionsMap, InputAction action) =>
            sdkInputActionsMap.TryGetValue(action, out UnityEngine.InputSystem.InputAction unityAction) && unityAction.WasReleasedThisFrame();
    }
}
