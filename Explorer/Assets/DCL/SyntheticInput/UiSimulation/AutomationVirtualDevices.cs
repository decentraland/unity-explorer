using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

namespace DCL.SyntheticInput.UiSimulation
{
    /// <summary>
    ///     A stock <see cref="Mouse" /> whose layout runs in the background, so the Input System keeps it enabled while
    ///     the client window is unfocused instead of resetting it along with the user's devices.
    /// </summary>
    [InputControlLayout(canRunInBackground = true, hideInUI = true)]
    public class DclAutomationMouse : Mouse { }

    /// <inheritdoc cref="DclAutomationMouse" />
    [InputControlLayout(canRunInBackground = true, hideInUI = true)]
    public class DclAutomationKeyboard : Keyboard { }

    /// <summary>Virtual mouse and keyboard, registered for the automation session and removed on dispose.</summary>
    public class AutomationVirtualDevices : IDisposable
    {
        private Vector2 lastMousePosition;

        public Mouse Mouse { get; }

        public Keyboard Keyboard { get; }

        public AutomationVirtualDevices()
        {
            InputSystem.RegisterLayout<DclAutomationMouse>();
            InputSystem.RegisterLayout<DclAutomationKeyboard>();

            Mouse = InputSystem.AddDevice<DclAutomationMouse>("DclAutomationMouse");
            Keyboard = InputSystem.AddDevice<DclAutomationKeyboard>("DclAutomationKeyboard");
        }

        /// <summary>Why a state queued now would not reach the game; false when it would.</summary>
        public bool TryGetDeliveryBlocker(UiDeviceGestureKind kind, [NotNullWhen(true)] out string? reason)
        {
            bool keyboard = kind == UiDeviceGestureKind.KeyPress;
            var editorWithholdsInput = false;

#if UNITY_EDITOR
            // Game view focus is what the Editor reports as application focus.
            editorWithholdsInput = Application.isPlaying && !Application.isFocused
                                   && InputSystem.settings.editorInputBehaviorInPlayMode != InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif

            reason = DescribeDeliveryBlocker(keyboard ? Keyboard.enabled : Mouse.enabled, editorWithholdsInput, keyboard);
            return reason != null;
        }

        /// <summary>
        ///     The Input System disables a device that cannot run in the background while the window is unfocused, and the
        ///     Editor routes pointer and keyboard events to itself while the Game view is unfocused (unless every device
        ///     always goes to the game). Either way a queued state never reaches player code.
        /// </summary>
        internal static string? DescribeDeliveryBlocker(bool deviceEnabled, bool editorWithholdsInput, bool keyboard)
        {
            const string SEMANTIC_PATH = "or use the semantic path (omit device / path:sdk), which does not go through the devices";

            if (!deviceEnabled)
                return $"the automation {(keyboard ? "keyboard" : "mouse")} is disabled: the Input System disables devices that cannot run in the background while the client window is unfocused; focus the Explorer window and retry, {SEMANTIC_PATH}";

            if (editorWithholdsInput)
                return "the Unity Editor's Game view is not focused, so the Input System routes pointer and keyboard device input to the Editor instead of the game "
                       + $"(Project Settings > Input System Package > Editor Input Behavior In Play Mode); click into the Game view and retry, {SEMANTIC_PATH}";

            return null;
        }

        public void Dispose()
        {
            if (Mouse.added)
                InputSystem.RemoveDevice(Mouse);

            if (Keyboard.added)
                InputSystem.RemoveDevice(Keyboard);
        }

        /// <summary>Position is in Unity screen coordinates (bottom-left origin).</summary>
        public void QueueMouseState(Vector2 position, bool leftPressed = false, bool rightPressed = false, Vector2 scroll = default)
        {
            var state = new MouseState
            {
                position = position,
                delta = position - lastMousePosition,
                scroll = scroll,
            };

            state = state.WithButton(MouseButton.Left, leftPressed).WithButton(MouseButton.Right, rightPressed);

            InputSystem.QueueStateEvent(Mouse, state);
            lastMousePosition = position;
        }

        /// <summary>Holds only <paramref name="pressedKey" />, so any key pressed before is released.</summary>
        public void QueueKeyState(Key? pressedKey)
        {
            var state = default(KeyboardState);

            if (pressedKey is { } key)
                state.Press(key);

            InputSystem.QueueStateEvent(Keyboard, state);
        }
    }
}
