using Arch.Core;
using DCL.ECSComponents;
using UnityEngine;

namespace DCL.Interaction.PlayerOriginated.Components
{
    /// <summary>
    ///     Single-frame instructions for the player-origin pointer pipeline, posted onto the player interaction
    ///     entity by an automation driver: aim the reticle ray at a world point and/or press or release a pointer
    ///     button as if the player did. PlayerOriginatedRaycastSystem reads the aim and echoes the point it consumed
    ///     in <see cref="PlayerOriginRaycastResultForSceneEntities.SyntheticAimPoint" /> (null on frames it guards
    ///     away); ProcessPointerEventsSystem reads the buttons, applies them under the same qualification gates as
    ///     real input, and clears the component. Both honour a post only during the frame recorded in
    ///     <see cref="PostedAtFrame" />, so a post that outlived the frame is discarded unread and nobody has to
    ///     sweep up instructions abandoned mid-pause. Posting is last-write-wins.
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
        ///     Whether the given scene entity may consume this post's button edge. An untargeted post permits
        ///     every entity; a targeted one permits exactly its own, matched by world reference and entity.
        /// </summary>
        public readonly bool MayConsume(World entityWorld, Entity entity) =>
            TargetEntity is not { } target || (ReferenceEquals(entityWorld, TargetWorld) && entity == target);
    }
}
