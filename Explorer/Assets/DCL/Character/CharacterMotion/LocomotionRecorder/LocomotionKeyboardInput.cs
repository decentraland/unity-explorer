using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

namespace DCL.CharacterMotion.LocomotionRecorder
{
    /// <summary>A stock <see cref="Keyboard" /> that keeps running while the window is unfocused, so the Input System does not reset it with the user's devices.</summary>
    [InputControlLayout(canRunInBackground = true, hideInUI = true)]
    public class LocomotionRecorderKeyboard : Keyboard { }

    /// <summary>
    ///     The <see cref="LocomotionInputSeam.Keyboard" /> seam: a virtual keyboard whose key state is queued right
    ///     before every dynamic Input System update, so the same frame's <c>UpdateInputMovementSystem</c> reads it through
    ///     the production DCLInput actions (WASD composite, Shift = sprint, Ctrl = walk), exactly as physical keys.
    ///     Being a real device it also raises the SDK's global input actions (IA_FORWARD...) a physical key does.
    ///     Created by the first keyboard suite and kept until the recorder is disposed.
    /// </summary>
    public sealed class LocomotionKeyboardInput : IDisposable
    {
        private readonly LocomotionSuiteSession session;
        private readonly Keyboard keyboard;

        private bool keysHeld;

        public LocomotionKeyboardInput(LocomotionSuiteSession session)
        {
            this.session = session;

            InputSystem.RegisterLayout<LocomotionRecorderKeyboard>();
            keyboard = InputSystem.AddDevice<LocomotionRecorderKeyboard>(nameof(LocomotionRecorderKeyboard));
            InputSystem.onBeforeUpdate += QueueKeys;
        }

        public void Dispose()
        {
            InputSystem.onBeforeUpdate -= QueueKeys;

            if (keyboard.added)
                InputSystem.RemoveDevice(keyboard);

            InputSystem.RemoveLayout(nameof(LocomotionRecorderKeyboard));
        }

        /// <summary>
        ///     The Editor routes keyboard events to itself while the Game view is unfocused (Input System's default
        ///     "pointers and keyboards respect Game view focus"); a player never withholds them.
        /// </summary>
        public static bool EditorWithholdsKeyboard()
        {
#if UNITY_EDITOR
            return Application.isPlaying && !Application.isFocused
                                         && InputSystem.settings.editorInputBehaviorInPlayMode != InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#else
            return false;
#endif
        }

        /// <summary>Events queued before an update are processed by that update, so the keys reach the actions in the frame they are meant for.</summary>
        private void QueueKeys()
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic)
                return;

            // A component-seam suite drives the movement input itself; the keyboard only lets go of what it held.
            if (session.Options.InputSeam != LocomotionInputSeam.Keyboard || !session.TryGetDrivenInput(out LocomotionInputStep step))
            {
                if (keysHeld)
                {
                    InputSystem.QueueStateEvent(keyboard, default(KeyboardState));
                    keysHeld = false;
                }

                return;
            }

            var state = default(KeyboardState);

            if (step.Fwd > 0) state.Press(Key.W);
            else if (step.Fwd < 0) state.Press(Key.S);

            if (step.Right > 0) state.Press(Key.D);
            else if (step.Right < 0) state.Press(Key.A);

            if (step.Sprint) state.Press(Key.LeftShift);
            if (step.Walk) state.Press(Key.LeftCtrl);

            InputSystem.QueueStateEvent(keyboard, state);
            keysHeld = true;
        }
    }
}
