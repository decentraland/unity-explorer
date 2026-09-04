using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DCL.SyntheticInput.UiSimulation
{
    /// <summary>
    ///     Virtual mouse and keyboard registered while automation is enabled. Layout-path bindings
    ///     ("&lt;Mouse&gt;/position", "&lt;Keyboard&gt;/e", ...) resolve them into BOTH input-action graphs — the
    ///     serialized asset driving the UI input module and the DCLInput.Instance clone gameplay polls — which is
    ///     the whole point of this path: an injected state event behaves like a real device for every consumer.
    ///     The devices stay enabled for the automation session and are removed on dispose.
    /// </summary>
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

        /// <summary>Queues one mouse state (position in Unity screen coordinates, bottom-left origin) for the next input update.</summary>
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

        /// <summary>Queues a keyboard state holding exactly the given key (or none), replacing the previous state.</summary>
        public void QueueKeyState(Key? pressedKey)
        {
            var state = default(KeyboardState);

            if (pressedKey is { } key)
                state.Press(key);

            InputSystem.QueueStateEvent(Keyboard, state);
        }
    }
}
