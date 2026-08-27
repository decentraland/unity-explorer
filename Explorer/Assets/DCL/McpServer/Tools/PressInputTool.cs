using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.McpServer.Utils;
using DCL.SyntheticInput;
using DCL.SyntheticInput.Components;
using Newtonsoft.Json.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Presses and releases an SDK input action with no aim of its own via
    ///     <see cref="SyntheticInputAgent.GlobalInputAsync" />: the edges fan out to the scene exactly like a real
    ///     key press — entity-bound when the reticle hovers a qualified entity, broadcast to the scene root
    ///     (a PBPointerEventsResult with no hit) otherwise.
    /// </summary>
    public class PressInputTool : McpTool
    {
        /// <summary>Wire-facing mirror of the SDK <see cref="InputAction" />s a scene can listen to globally.</summary>
        private enum SdkAction : byte
        {
            POINTER,
            PRIMARY,
            SECONDARY,
            JUMP,
            FORWARD,
            BACKWARD,
            RIGHT,
            LEFT,
            ACTION_3,
            ACTION_4,
            ACTION_5,
            ACTION_6,
            WALK,
            MODIFIER,
        }

        private const float MAX_HOLD_SECONDS = 30f;

        private readonly SyntheticInputAgent syntheticInput;

        public override string Name => "press_input";

        public override string Description =>
            "Press and release an SDK input action (IA_PRIMARY, IA_SECONDARY, IA_ACTION_3..6, movement actions, ...) so the "
            + "scene observes it exactly like the real key: entity-bound on the entity under the reticle when one is hovered "
            + "in range, otherwise as a global PBPointerEventsResult on the scene root. The release lands on a later scene "
            + "tick; holdSeconds keeps the action held between press and release. This does NOT move the player — use walk for that.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Enum<SdkAction>("action", "Which SDK input action to press.", isRequired: true)
                  .Number("holdSeconds", "Seconds between the press and the release. Default 0 (release on the next scene tick), max 30.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public PressInputTool(SyntheticInputAgent syntheticInput)
        {
            this.syntheticInput = syntheticInput;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!arguments.TryGetEnum("action", out SdkAction action))
                return McpToolResult.Error("action is required (e.g. primary, secondary, action_3).");

            float holdSeconds = Mathf.Clamp(arguments.GetFloat("holdSeconds", 0f), 0f, MAX_HOLD_SECONDS);

            SyntheticPointerResult result = await syntheticInput.GlobalInputAsync(ToInputAction(action), holdSeconds, ct);

            if (result.TimedOut)
                return McpToolResult.Error($"press_input did not complete within {holdSeconds + SyntheticInputAgent.COMPLETION_GRACE_SEC}s (is the simulation paused?).");

            if (result.FailureReason != null && !result.Hit)
                return McpToolResult.Error($"press_input was not delivered: {result.FailureReason}");

            var json = new JObject
            {
                // A qualified hovered entity received the events entity-bound (which suppresses the scene-root
                // broadcast for that frame); otherwise the scene root got the global events.
                ["entityBound"] = result.Hit,
            };

            if (result.Hit)
            {
                json["entityId"] = result.SceneEntityId;
                json["crdtEntityId"] = result.CrdtEntityId;

                if (result.HoverText != null)
                    json["hoverText"] = result.HoverText;
            }

            if (result.FailureReason != null)
                json["warning"] = result.FailureReason;

            return McpToolResult.Json(json);
        }

        private static InputAction ToInputAction(SdkAction action) =>
            action switch
            {
                SdkAction.POINTER => InputAction.IaPointer,
                SdkAction.PRIMARY => InputAction.IaPrimary,
                SdkAction.SECONDARY => InputAction.IaSecondary,
                SdkAction.JUMP => InputAction.IaJump,
                SdkAction.FORWARD => InputAction.IaForward,
                SdkAction.BACKWARD => InputAction.IaBackward,
                SdkAction.RIGHT => InputAction.IaRight,
                SdkAction.LEFT => InputAction.IaLeft,
                SdkAction.ACTION_3 => InputAction.IaAction3,
                SdkAction.ACTION_4 => InputAction.IaAction4,
                SdkAction.ACTION_5 => InputAction.IaAction5,
                SdkAction.ACTION_6 => InputAction.IaAction6,
                SdkAction.WALK => InputAction.IaWalk,
                _ => InputAction.IaModifier,
            };
    }
}
