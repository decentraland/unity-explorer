using Arch.Core;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.Diagnostics;
using DCL.Input;
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
    ///     mouse or keyboard. While a pointer gesture runs, the OS-cursor warps are suppressed via
    ///     <see cref="SyntheticCursorState" /> so the cursor systems do not fight the injected positions.
    ///     Pointer gestures require a free cursor: with the cursor locked or panning the on-screen UI is not in a
    ///     clickable state, so the gesture fails instead of silently mutating the lock.
    /// </summary>
    [UpdateInGroup(typeof(InputGroup))]
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

            if (gesture.Phase == UiGesturePhase.NotStarted && gesture.Kind != UiDeviceGestureKind.KeyPress && IsCursorCaptured())
            {
                // The gesture is copied out before the structural removal; no component refs are touched afterwards.
                EcsRequest.CompleteAndRemove(World, playerEntity, gesture,
                    new UiGestureResult { Ok = false, FailureReason = "the cursor is locked or panning — pointer gestures need a free cursor" });

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

        private bool IsCursorCaptured()
        {
            if (!World.TryGet(camera, out CursorComponent cursor))
                return false;

            return cursor.CursorState is CursorState.Locked or CursorState.Panning;
        }

        /// <summary>Interpolates the pointer from From to To over the duration; Hover simply uses From == To.</summary>
        private bool StepMove(ref UiDeviceGestureRequest gesture)
        {
            gesture.Phase = UiGesturePhase.Moving;
            int duration = Mathf.Max(1, gesture.DurationFrames);

            float progress = Mathf.Clamp01((float)gesture.FrameIndex / duration);
            devices.QueueMouseState(Vector2.Lerp(gesture.From, gesture.To, progress));

            return gesture.FrameIndex++ >= duration;
        }

        /// <summary>Move to the point, press, release — the button edges land on separate frames like a real click.</summary>
        private bool StepClick(ref UiDeviceGestureRequest gesture)
        {
            switch (gesture.Phase)
            {
                case UiGesturePhase.NotStarted:
                    devices.QueueMouseState(gesture.To);
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
                    devices.QueueMouseState(gesture.From);
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
            devices.QueueMouseState(position,
                leftPressed: button == MouseButton.Left && pressed,
                rightPressed: button == MouseButton.Right && pressed);
    }
}
