using Cysharp.Threading.Tasks;
using DCL.CharacterMotion.Components;
using DCL.SyntheticInput.Core;
using UnityEngine;

namespace DCL.SyntheticInput.Components
{
    /// <summary>
    ///     Held movement input requested by an automation driver. While present on the player entity,
    ///     <see cref="SyntheticMovementInputSystem" /> re-asserts it into <see cref="MovementInputComponent" /> every frame.
    /// </summary>
    public struct SyntheticMovementIntent : IEcsRequest<SyntheticInputDelivery>
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
        ///     By default the hold obeys the scene's InputModifier locks exactly like real input (a movement lock
        ///     idles it, disabled kinds degrade through the same fallback table, a jump lock drops the jump).
        ///     Set for deliberate test escapes that must move the player regardless.
        /// </summary>
        public bool IgnoreInputModifiers;

        /// <summary>
        ///     Completed by the system when the hold expires or is preempted by a newer request.
        /// </summary>
        public UniTaskCompletionSource<SyntheticInputDelivery>? Completion { get; set; }
    }

    /// <summary>How a held synthetic input request ended.</summary>
    public enum SyntheticInputDelivery : byte
    {
        /// <summary>The hold ran to its full duration.</summary>
        Completed,

        /// <summary>A newer request replaced this one before it finished.</summary>
        Preempted,

        /// <summary>The simulation never completed the request within the driver-side timeout; the request was abandoned.</summary>
        TimedOut,
    }
}
