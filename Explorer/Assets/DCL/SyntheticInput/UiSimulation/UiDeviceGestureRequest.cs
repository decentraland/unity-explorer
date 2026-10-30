using Cysharp.Threading.Tasks;
using DCL.SyntheticInput.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DCL.SyntheticInput.UiSimulation
{
    public enum UiDeviceGestureKind : byte
    {
        /// <summary>Moves the pointer to <see cref="UiDeviceGestureRequest.To" /> over the duration.</summary>
        MoveTo,

        /// <summary>
        ///     Moves to <see cref="UiDeviceGestureRequest.To" />, then presses and releases the button on separate
        ///     frames.
        /// </summary>
        Click,

        /// <summary>
        ///     Presses at <see cref="UiDeviceGestureRequest.From" />, drags to <see cref="UiDeviceGestureRequest.To" />
        ///     over the duration, then releases.
        /// </summary>
        Drag,

        /// <summary>
        ///     Moves the pointer from <see cref="UiDeviceGestureRequest.From" /> to <see cref="UiDeviceGestureRequest.To" />
        ///     over the duration, buttons up; equal points rest it there. The pointer stays after the gesture completes.
        /// </summary>
        Hover,

        /// <summary>Presses and releases <see cref="UiDeviceGestureRequest.Key" />, held for the duration.</summary>
        KeyPress,
    }

    public enum UiGesturePhase : byte
    {
        NotStarted,
        Moving,
        Pressed,
        Dragging,
        Holding,
        Released,
    }

    /// <summary>
    ///     A multi-frame virtual-device gesture, driven one input state per frame. Positions are Unity screen
    ///     coordinates (bottom-left origin).
    /// </summary>
    public struct UiDeviceGestureRequest : IEcsRequest<UiGestureResult>
    {
        public UiDeviceGestureKind Kind;
        public Vector2 From;
        public Vector2 To;

        public int DurationFrames;

        public MouseButton Button;
        public Key Key;

        public UiGesturePhase Phase;
        public int FrameIndex;

        public UniTaskCompletionSource<UiGestureResult>? Completion { get; set; }
    }

    public struct UiGestureResult
    {
        public bool Ok;
        public string? FailureReason;
    }
}
