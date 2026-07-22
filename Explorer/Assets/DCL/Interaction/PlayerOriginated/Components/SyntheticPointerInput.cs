using DCL.ECSComponents;
using UnityEngine;

namespace DCL.Interaction.PlayerOriginated.Components
{
    /// <summary>
    ///     <para>
    ///         Single-frame instructions for the player-origin pointer pipeline, posted onto the player
    ///         interaction entity by an automation driver (e.g. the MCP server): aim the reticle ray at a world
    ///         point and/or press or release a pointer button as if the player did.
    ///     </para>
    ///     <para>
    ///         PlayerOriginatedRaycastSystem reads the aim when building the ray and echoes the point it consumed
    ///         in <see cref="PlayerOriginRaycastResultForSceneEntities.SyntheticAimPoint" /> (null on frames it
    ///         guards away), so drivers can tell whether their aim was processed; ProcessPointerEventsSystem
    ///         reads the buttons, applies them under the same qualification gates as real input, and clears the
    ///         component.
    ///     </para>
    ///     <para>
    ///         Both readers honor a post only during the frame recorded in <see cref="PostedAtFrame" />: a post
    ///         that survived longer (the pipeline skipped frames) is discarded unread, so no component owner has
    ///         to sweep up instructions abandoned mid-pause. Posting is last-write-wins — at most one driver may
    ///         steer the pipeline at a time.
    ///     </para>
    /// </summary>
    public struct SyntheticPointerInput
    {
        /// <summary>
        ///     Squared minimum distance from the camera origin to <see cref="AimPoint" />; any closer and no aim
        ///     ray can be built, so the pipeline skips the raycast and drivers must fail such a request upfront.
        /// </summary>
        public const float MIN_AIM_DISTANCE_SQR = 0.0001f;

        /// <summary>World point the reticle ray is forced to pass through this frame; null keeps the cursor ray.</summary>
        public Vector3? AimPoint;

        /// <summary>Button reported as pressed this frame.</summary>
        public InputAction? PressButton;

        /// <summary>Button reported as released this frame.</summary>
        public InputAction? ReleaseButton;

        /// <summary><see cref="UnityEngine.Time.frameCount" /> at the moment of posting; every poster must stamp it.</summary>
        public int PostedAtFrame;

        /// <summary>The instructions are valid only while this holds; readers treat a stale post as absent.</summary>
        public bool IsPostedThisFrame => PostedAtFrame == UnityEngine.Time.frameCount;
    }
}
