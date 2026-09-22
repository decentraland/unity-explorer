using Cysharp.Threading.Tasks;
using DCL.CharacterMotion.Components;
using DCL.SyntheticInput.Core;
using UnityEngine;

namespace DCL.SyntheticInput.Components
{
    /// <summary>Held movement input requested by an automation driver, present on the player entity while it lasts.</summary>
    public struct SyntheticMovementIntent : IEcsRequest<SyntheticInputDelivery>
    {
        /// <summary>Normalized camera-relative axes (x = strafe, y = forward).</summary>
        public Vector2 Axes;

        public MovementKind Kind;

        /// <summary>Value of Time.time at which the hold expires.</summary>
        public float EndTime;

        /// <summary>Requests one jump. Cleared by the system after the first frame of the hold.</summary>
        public bool JumpRequested;

        /// <summary>When true, the hold bypasses the scene's InputModifier locks.</summary>
        public bool IgnoreInputModifiers;

        public UniTaskCompletionSource<SyntheticInputDelivery>? Completion { get; set; }
    }

    /// <summary>How a held synthetic input request ended.</summary>
    public enum SyntheticInputDelivery : byte
    {
        /// <summary>The hold ran to its full duration.</summary>
        Completed,

        /// <summary>A newer request replaced this one before it finished.</summary>
        Preempted,

        /// <summary>The request was abandoned because it did not complete within the driver-side timeout.</summary>
        TimedOut,
    }
}
