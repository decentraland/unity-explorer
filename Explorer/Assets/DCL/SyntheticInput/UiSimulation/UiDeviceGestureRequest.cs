using Cysharp.Threading.Tasks;
using DCL.SyntheticInput.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DCL.SyntheticInput.UiSimulation
{
    public enum UiDeviceGestureKind : byte
    {
        /// <summary>Move the virtual pointer to <see cref="UiDeviceGestureRequest.To" /> over the duration.</summary>
        MoveTo,

        /// <summary>Move to <see cref="UiDeviceGestureRequest.To" />, then press and release the button on separate frames.</summary>
        Click,

        /// <summary>Press at <see cref="UiDeviceGestureRequest.From" />, drag to <see cref="UiDeviceGestureRequest.To" /> over the duration, release.</summary>
        Drag,

        /// <summary>Hold the pointer at <see cref="UiDeviceGestureRequest.To" /> for the duration (hover-timing UI).</summary>
        Hover,

        /// <summary>Press and release <see cref="UiDeviceGestureRequest.Key" />; a duration holds it in between.</summary>
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
    ///     A multi-frame virtual-device gesture, driven one input state per frame by UiVirtualDeviceGestureSystem.
    ///     Positions are Unity screen coordinates (bottom-left origin). The phase machine lives inside the
    ///     component; the system stays stateless.
    /// </summary>
    public struct UiDeviceGestureRequest : IEcsRequest<UiGestureResult>
    {
        public UiDeviceGestureKind Kind;
        public Vector2 From;
        public Vector2 To;

        /// <summary>Frames spent moving/dragging/holding; clamped to at least 1 by the consuming system.</summary>
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
