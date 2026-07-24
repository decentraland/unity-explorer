using Cysharp.Threading.Tasks;
using DCL.CharacterMotion.Components;
using DCL.McpServer.Core;
using UnityEngine;

namespace DCL.McpServer.Components
{
    /// <summary>
    ///     Held movement input requested by the MCP walk tool. While present on the player entity,
    ///     <see cref="McpInputOverrideSystem" /> re-asserts it into <see cref="MovementInputComponent" /> every frame.
    /// </summary>
    public struct McpMovementOverride : IMcpEcsRequest<AsyncUnit>
    {
        /// <summary>
        ///     Normalized camera-relative axes (x = strafe, y = forward).
        /// </summary>
        public Vector2 Axes;

        public MovementKind Kind;

        /// <summary>
        ///     Value of Time.time at which the hold expires.
        /// </summary>
        public float EndTime;

        /// <summary>
        ///     Requests a single jump; consumed on the first frame of the hold.
        /// </summary>
        public bool JumpRequested;

        /// <summary>
        ///     Completed by the system when the hold expires or is preempted by a newer request.
        /// </summary>
        public UniTaskCompletionSource<AsyncUnit>? Completion { get; set; }
    }
}
