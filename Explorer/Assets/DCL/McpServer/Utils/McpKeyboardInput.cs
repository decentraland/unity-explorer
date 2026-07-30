#if MCP_TEST_AUTOMATION
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DCL.McpServer.Utils
{
    /// <summary>
    ///     Presses keys on the client by queueing keyboard state onto the Input System device the Explorer already
    ///     reads. The Explorer has no shared "key intent" seam to borrow the way <c>walk</c> borrows
    ///     <c>MovementInputComponent</c>: every shortcut subscribes to its own <c>DCLInput</c> action bound to
    ///     <c>&lt;Keyboard&gt;/…</c>, so the device event queue is the one point that reaches all of them — gameplay
    ///     shortcuts, the UI map and focused text fields alike — with the real action maps' enable/disable gating still
    ///     applying. A key pressed while its action map is disabled therefore does nothing, exactly as for a user.
    ///     <para>
    ///         State events carry the whole keyboard, so <see cref="HELD" /> tracks what this server holds and every
    ///         event re-asserts it. A physical key held by a human at the same moment is released as a side effect;
    ///         that is acceptable on a machine being driven by an agent.
    ///     </para>
    ///     Main thread only.
    /// </summary>
    public static class McpKeyboardInput
    {
        private static readonly HashSet<Key> HELD = new ();

        /// <summary>
        ///     Parses a key name case-insensitively as a <see cref="Key" /> member ("Escape", "Space", "X", "F5",
        ///     "LeftShift", "Numpad1"). The Input System's own names are the vocabulary, so there is no second spelling
        ///     of a key here to drift out of sync with it.
        /// </summary>
        public static bool TryResolveKey(string? name, out Key key)
        {
            key = Key.None;

            if (string.IsNullOrWhiteSpace(name))
                return false;

            string trimmed = name.Trim();

            // Enum.TryParse also reads the underlying number ("42", "-1") and comma-separated lists ("X,Space",
            // which ORs into an unrelated member); every Key member name starts with a letter and holds no comma.
            return char.IsLetter(trimmed[0])
                   && trimmed.IndexOf(',') < 0
                   && Enum.TryParse(trimmed, true, out key)
                   && key != Key.None;
        }

        /// <summary>Presses <paramref name="key" /> and keeps it down until <see cref="Release" />.</summary>
        public static void Press(Key key)
        {
            HELD.Add(key);
            Flush();
        }

        /// <summary>Releases <paramref name="key" />, leaving any other key this server holds down.</summary>
        public static void Release(Key key)
        {
            HELD.Remove(key);
            Flush();
        }

        /// <summary>
        ///     Releases every key this server holds, leaving the keyboard as it was before the server pressed
        ///     anything. Does nothing while the set is already empty, so it never adds a keyboard device just to
        ///     clear one.
        /// </summary>
        public static void Reset()
        {
            if (HELD.Count == 0)
                return;

            HELD.Clear();
            Flush();
        }

        /// <summary>The keys this server is currently holding down, for the tool result.</summary>
        public static JArray HeldKeys()
        {
            var array = new JArray();

            foreach (Key key in HELD)
                array.Add(key.ToString());

            return array;
        }

        /// <summary>
        ///     Queues the full held-key state onto the keyboard device, adding a virtual one when the process has no
        ///     keyboard at all (a headless or fully detached build). The queued event is consumed by the next Input
        ///     System update in the player loop.
        /// </summary>
        private static void Flush()
        {
            Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            var state = new KeyboardState();

            foreach (Key key in HELD)
                state.Set(key, true);

            InputSystem.QueueStateEvent(keyboard, state);
        }
    }
}
#endif
