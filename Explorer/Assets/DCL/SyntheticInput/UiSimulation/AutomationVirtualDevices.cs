using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DCL.SyntheticInput.UiSimulation
{
    /// <summary>Virtual mouse and keyboard, registered for the automation session and removed on dispose.</summary>
    public class AutomationVirtualDevices : IDisposable
    {
        private Vector2 lastMousePosition;

        public Mouse Mouse { get; }

        public Keyboard Keyboard { get; }

        public AutomationVirtualDevices()
        {
            Mouse = InputSystem.AddDevice<Mouse>("DclAutomationMouse");
            Keyboard = InputSystem.AddDevice<Keyboard>("DclAutomationKeyboard");
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
