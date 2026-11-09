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
using Utility.Arch;

namespace DCL.SyntheticInput.Systems
{
    /// <summary>
    ///     Replays a <see cref="UiDeviceGestureRequest" /> one input state per frame through the automation virtual devices.
    ///     Each pointer position is also written into <see cref="SyntheticCursorOverride" />, which
    ///     <see cref="UpdateCursorInputSystem" /> reads the same frame.
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

            // Every system that writes the override installs it, so none depends on a sibling being registered.
            World.AddOrSet(camera, SyntheticCursorOverride.Inactive);
        }

        protected override void Update(float t)
        {
            ref UiDeviceGestureRequest gesture = ref World.TryGetRef<UiDeviceGestureRequest>(playerEntity, out bool exists);

            if (!exists)
                return;

            if (gesture.Kind != UiDeviceGestureKind.KeyPress && TryGetCapturedCursorState(out CursorState capturedState))
            {
                EcsRequest.CompleteAndRemove(World, playerEntity, gesture,
                    new UiGestureResult { Ok = false, FailureReason = CaptureFailureReason(in gesture, capturedState) });

                return;
            }

            if (gesture.Kind != UiDeviceGestureKind.KeyPress)
                World.Get<SyntheticCursorOverride>(camera).AssertSuppressionThisFrame();

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

        private static string CaptureFailureReason(in UiDeviceGestureRequest gesture, CursorState capturedState) =>
            gesture.Phase == UiGesturePhase.NotStarted
                ? "the cursor is locked or panning, so pointer gestures cannot run"
                : capturedState == CursorState.Panning
                    ? "the drag panned the camera instead of dragging: a held button dragged across the world pans the camera. Drag over UI, or use sweep_pointer to hold a button while the camera turns"
                    : "the cursor was locked mid-gesture, so the rest of the gesture was not delivered";

        private bool StepMove(ref UiDeviceGestureRequest gesture)
        {
            gesture.Phase = UiGesturePhase.Moving;
            int duration = Mathf.Max(1, gesture.DurationFrames);

            float progress = Mathf.Clamp01((float)gesture.FrameIndex / duration);
            QueueMouse(Vector2.Lerp(gesture.From, gesture.To, progress));

            return gesture.FrameIndex++ >= duration;
        }

        // The press and the release land on separate frames, as with a real click.
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

        // Every pointer state goes through here, so the cursor override never lags behind the device.
        private void QueueMouse(Vector2 position, bool leftPressed = false, bool rightPressed = false)
        {
            devices.QueueMouseState(position, leftPressed, rightPressed);
            World.Get<SyntheticCursorOverride>(camera).AssertPointerPositionThisFrame(position);
        }
    }
}
