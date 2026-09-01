using Arch.Core;
using Arch.SystemGroups;
using DCL.Character.CharacterCamera.Components;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Systems;
using DCL.SyntheticInput.Core;
using DCL.SyntheticInput.UiSimulation;
using ECS.Abstract;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace DCL.SyntheticInput.Systems
{
    /// <summary>
    ///     Drives a <see cref="UiDeviceGestureRequest" /> one input state per frame through the automation
    ///     virtual devices, so uGUI, UI Toolkit and gameplay all observe the gesture exactly as they would a real
    ///     mouse or keyboard. Every queued pointer position is also published through
    ///     <see cref="SyntheticCursorState" />, which is what makes the cursor systems follow the gesture instead
    ///     of the hardware mouse (and skip their OS-cursor warps) — hence the ordering against
    ///     <see cref="UpdateCursorInputSystem" />, which reads that position the same frame.
    ///     Pointer gestures require a free cursor: with the cursor locked or panning the on-screen UI is not in a
    ///     clickable state, so the gesture fails instead of silently mutating the lock. That is re-checked every
    ///     frame, because a left-button drag over the world turns the cursor to panning mid-gesture (TemporalLock
    ///     is bound to the left mouse button) and the drag a caller asked for never happens.
    /// </summary>
    [UpdateInGroup(typeof(InputGroup))]
    [UpdateBefore(typeof(UpdateCursorInputSystem))]
    [LogCategory(ReportCategory.SYNTHETIC_INPUT)]
    public partial class UiVirtualDeviceGestureSystem : BaseUnityLoopSystem
    {
        private readonly Entity playerEntity;
        private readonly AutomationVirtualDevices devices;

        private SingleInstanceEntity camera;

        internal UiVirtualDeviceGestureSystem(World world, Entity playerEntity, AutomationVirtualDevices devices) : base(world)
        {
            this.playerEntity = playerEntity;
            this.devices = devices;
        }

        public override void Initialize()
        {
            base.Initialize();
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            ref UiDeviceGestureRequest gesture = ref World.TryGetRef<UiDeviceGestureRequest>(playerEntity, out bool exists);

            if (!exists)
                return;

            if (gesture.Kind != UiDeviceGestureKind.KeyPress && TryGetCapturedCursorState(out CursorState capturedState))
            {
                // The gesture is copied out before the structural removal; no component refs are touched afterwards.
                EcsRequest.CompleteAndRemove(World, playerEntity, gesture,
                    new UiGestureResult { Ok = false, FailureReason = CaptureFailureReason(in gesture, capturedState) });

                return;
            }

            if (gesture.Kind != UiDeviceGestureKind.KeyPress)
                SyntheticCursorState.AssertSuppressionThisFrame();

            bool done = gesture.Kind switch
                        {
                            UiDeviceGestureKind.MoveTo => StepMove(ref gesture),
                            UiDeviceGestureKind.Hover => StepMove(ref gesture),
                            UiDeviceGestureKind.Click => StepClick(ref gesture),
                            UiDeviceGestureKind.Drag => StepDrag(ref gesture),
                            _ => StepKeyPress(ref gesture),
                        };

            if (done)
                EcsRequest.CompleteAndRemove(World, playerEntity, gesture, new UiGestureResult { Ok = true });
        }

        private bool TryGetCapturedCursorState(out CursorState capturedState)
        {
            capturedState = CursorState.Free;

            if (!World.TryGet(camera, out CursorComponent cursor) || cursor.CursorState is not (CursorState.Locked or CursorState.Panning))
                return false;

            capturedState = cursor.CursorState;
            return true;
        }

        /// <summary>
        ///     Why a pointer gesture cannot run under a captured cursor. A gesture that started and then found the
        ///     cursor panning was itself the cause: a held left button over the world is the camera-pan gesture
        ///     (TemporalLock binds the left mouse button), so the caller's drag became a camera pan and saying "ok"
        ///     would report a delivery that never happened.
        /// </summary>
        private static string CaptureFailureReason(in UiDeviceGestureRequest gesture, CursorState capturedState) =>
            gesture.Phase == UiGesturePhase.NotStarted
                ? "the cursor is locked or panning — pointer gestures need a free cursor"
                : capturedState == CursorState.Panning
                    ? "the drag panned the camera instead of dragging: a held button dragged across the world pans, exactly as it does for a human — drag over UI, or use sweep_pointer to hold a button while the camera turns"
                    : "the cursor was locked mid-gesture, so the rest of the gesture was not delivered";

        /// <summary>Interpolates the pointer from From to To over the duration; Hover simply uses From == To.</summary>
        private bool StepMove(ref UiDeviceGestureRequest gesture)
        {
            gesture.Phase = UiGesturePhase.Moving;
            int duration = Mathf.Max(1, gesture.DurationFrames);

            float progress = Mathf.Clamp01((float)gesture.FrameIndex / duration);
            QueueMouse(Vector2.Lerp(gesture.From, gesture.To, progress));

            return gesture.FrameIndex++ >= duration;
        }

        /// <summary>Move to the point, press, release — the button edges land on separate frames like a real click.</summary>
        private bool StepClick(ref UiDeviceGestureRequest gesture)
        {
            switch (gesture.Phase)
            {
                case UiGesturePhase.NotStarted:
                    QueueMouse(gesture.To);
                    gesture.Phase = UiGesturePhase.Moving;
                    return false;
                case UiGesturePhase.Moving:
                    QueueButtonAt(gesture.To, gesture.Button, pressed: true);
                    gesture.Phase = UiGesturePhase.Pressed;
                    return false;
                default:
                    QueueButtonAt(gesture.To, gesture.Button, pressed: false);
                    gesture.Phase = UiGesturePhase.Released;
                    return true;
            }
        }

        private bool StepDrag(ref UiDeviceGestureRequest gesture)
        {
            switch (gesture.Phase)
            {
                case UiGesturePhase.NotStarted:
                    QueueMouse(gesture.From);
                    gesture.Phase = UiGesturePhase.Moving;
                    return false;
                case UiGesturePhase.Moving:
                    QueueButtonAt(gesture.From, gesture.Button, pressed: true);
                    gesture.Phase = UiGesturePhase.Dragging;
                    return false;
                case UiGesturePhase.Dragging:
                {
                    int duration = Mathf.Max(1, gesture.DurationFrames);
                    float progress = Mathf.Clamp01((float)++gesture.FrameIndex / duration);
                    QueueButtonAt(Vector2.Lerp(gesture.From, gesture.To, progress), gesture.Button, pressed: true);

                    if (gesture.FrameIndex >= duration)
                        gesture.Phase = UiGesturePhase.Holding;

                    return false;
                }
                default:
                    QueueButtonAt(gesture.To, gesture.Button, pressed: false);
                    gesture.Phase = UiGesturePhase.Released;
                    return true;
            }
        }

        private bool StepKeyPress(ref UiDeviceGestureRequest gesture)
        {
            switch (gesture.Phase)
            {
                case UiGesturePhase.NotStarted:
                    devices.QueueKeyState(gesture.Key);
                    gesture.Phase = UiGesturePhase.Holding;
                    return false;
                case UiGesturePhase.Holding:
                    if (gesture.FrameIndex++ < gesture.DurationFrames)
                        return false;

                    devices.QueueKeyState(null);
                    gesture.Phase = UiGesturePhase.Released;
                    return true;
                default:
                    devices.QueueKeyState(null);
                    gesture.Phase = UiGesturePhase.Released;
                    return true;
            }
        }

        private void QueueButtonAt(Vector2 position, MouseButton button, bool pressed) =>
            QueueMouse(position,
                leftPressed: button == MouseButton.Left && pressed,
                rightPressed: button == MouseButton.Right && pressed);

        /// <summary>
        ///     The single door every pointer state goes through: the device gets the state, and the cursor systems
        ///     get the position. Routing both here is what stops a phase from moving the pointer without telling
        ///     the cursor — the failure that left the world reticle behind while the UI stack followed the gesture.
        /// </summary>
        private void QueueMouse(Vector2 position, bool leftPressed = false, bool rightPressed = false)
        {
            devices.QueueMouseState(position, leftPressed, rightPressed);
            SyntheticCursorState.AssertPointerPositionThisFrame(position);
        }
    }
}
