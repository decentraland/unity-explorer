#if MCP_TEST_AUTOMATION
using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Presses a key on the Input System keyboard device the Explorer reads, so every consumer of the corresponding
    ///     <c>DCLInput</c> action sees it — the HUD shortcuts, the UI map and a focused text field alike. See
    ///     <see cref="McpKeyboardInput" /> for why the device queue is the seam rather than a per-action shortcut.
    /// </summary>
    public class PressKeyTool : McpTool
    {
        /// <summary>Wire-facing gesture kinds: a full tap, or a single press/release leg for held keys and chords.</summary>
        private enum KeyEvent : byte
        {
            /// <summary>Key down, held for the requested duration, then up.</summary>
            Press,

            /// <summary>Press-only leg; the key stays down until an "up" call releases it.</summary>
            [UsedImplicitly]
            Down,
            Up,
        }

        private const float DEFAULT_SECONDS = 0.1f;
        private const float MAX_SECONDS = 5f;

        public override string Name => "press_key";

        public override string Description =>
            "Press a keyboard key on the client. The name is any Input System key "
            + "member, case-insensitive: Escape, Space, X, Tab, Enter, F5, LeftShift, Numpad1, UpArrow, Digit1. The "
            + "event goes onto "
            + "the keyboard device, so the real action maps still gate it — a HUD shortcut does nothing while its map is "
            + "disabled (a modal open, a text field focused), exactly as for a user. eventType down leaves the key held "
            + "so chords are possible; up releases it. Read the effect back with get_ui_state or screenshot.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.String("key", "Input System key name, e.g. Escape, Space, I, Tab, F5, LeftShift, UpArrow.", isRequired: true)
                  .Enum<KeyEvent>("eventType", "press = down, hold, then up. Default press.")
                  .Number("seconds", $"How long a press holds the key. Default {DEFAULT_SECONDS}, max {MAX_SECONDS}.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            string name = arguments.GetString("key", string.Empty);

            if (!McpKeyboardInput.TryResolveKey(name, out Key key))
                return McpToolResult.Error($"Unknown key '{name}'. Use an Input System key name: Escape, Space, X, Tab, Enter, F5, LeftShift, Numpad1, UpArrow, Digit1.");

            if (!arguments.TryGetEnum("eventType", KeyEvent.Press, out KeyEvent keyEvent))
                return McpToolResult.Error("eventType must be one of: press, down, up.");

            float seconds = Mathf.Clamp(arguments.GetFloat("seconds", DEFAULT_SECONDS), 0f, MAX_SECONDS);

            if (keyEvent == KeyEvent.Up)
                McpKeyboardInput.Release(key);
            else
                McpKeyboardInput.Press(key);

            try
            {
                // The queued state is consumed by the next Input System update, so let the player loop run before reporting.
                await UniTask.DelayFrame(1, cancellationToken: ct);

                // Unscaled, so a hold still ends while the simulation is paused or slowed.
                if (keyEvent == KeyEvent.Press && seconds > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.UnscaledDeltaTime, cancellationToken: ct);
            }
            finally
            {
                // A cancelled hold — the dispatcher's per-call timeout, or server teardown — must still lift the key:
                // the held set outlives this call and every later keyboard event re-asserts it.
                if (keyEvent == KeyEvent.Press)
                    McpKeyboardInput.Release(key);
            }

            if (keyEvent == KeyEvent.Press)
                await UniTask.DelayFrame(1, cancellationToken: ct);

            var result = new JObject
            {
                // Not McpWireEnum: key is a free-form string whose vocabulary is the Input System member names, so it
                // goes back out spelled exactly as the argument accepts it.
                ["key"] = key.ToString(),
                ["eventType"] = McpWireEnum<KeyEvent>.ToWire(keyEvent),
                ["heldKeys"] = McpKeyboardInput.HeldKeys(),
            };

            if (keyEvent == KeyEvent.Press)
                result["heldSeconds"] = Math.Round(seconds, 2);

            return McpToolResult.Json(result);
        }
    }
}
#endif
