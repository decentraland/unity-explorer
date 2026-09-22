using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.SyntheticInput;
using DCL.SyntheticInput.Components;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>Presses and releases an SDK input action, aimed at an entity or broadcast to the scene root.</summary>
    public class PressInputTool : McpTool
    {
        /// <summary>
        ///     The member names are the wire contract: McpWireEnum snake-cases them, so ACTION_3 yields "action_3"
        ///     while a PascalCase Action3 would yield "action3".
        /// </summary>
        [SuppressMessage("ReSharper", "InconsistentNaming")]
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
            + "scene observes it exactly like the real key. Without an aim it arrives as a global PBPointerEventsResult on "
            + "the scene root; pass entityId or an x/y/z world aim point to steer the reticle at a target so it arrives "
            + "entity-bound on it instead (and suppresses the scene-root broadcast for that frame, like a key pressed while "
            + "looking at the entity). A press that names an entityId reaches that entity or nobody: if something blocks the "
            + "line of sight or the target does not qualify, the result says so and the scene sees no press at all, "
            + "root included. The release lands on a later scene tick; holdSeconds keeps the action held between "
            + "press and release. This does NOT move the player — use walk for that.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Enum<SdkAction>("action", "Which SDK input action to press.", isRequired: true)
                  .Number("holdSeconds", "Seconds between the press and the release. Default 0 (release on the next scene tick), max 30.")
                  .Integer("entityId", "Aim the reticle at this entity for the gesture (from list_scene_entities) so the action lands entity-bound on it. Omit for a scene-root broadcast.")
                  .Number("x", "World-space aim point; an alternative to entityId (and it overrides the aim at the entity's collider center).")
                  .Number("y")
                  .Number("z")
                  .String("sceneId", "Pin the gesture to this scene (id from get_scene_state): it fails instead of landing in another scene if the player moved.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public PressInputTool(SyntheticInputAgent syntheticInput)
        {
            this.syntheticInput = syntheticInput;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!arguments.TryGetEnum("action", out SdkAction action))
                return McpToolResult.Error(arguments.EnumArgumentError<SdkAction>("action"));

            float holdSeconds = Mathf.Clamp(arguments.GetFloat("holdSeconds", 0f), 0f, MAX_HOLD_SECONDS);

            if (!PointerArgs.TryParseAim(arguments, requireTarget: false, out PointerAim aim, out string? aimError))
                return McpToolResult.Error(aimError!);

            SyntheticPointerResult result = await syntheticInput.GlobalInputAsync(ToInputAction(action), holdSeconds, aim, ct);

            if (result.TimedOut)
                return McpToolResult.Error($"press_input did not complete within {holdSeconds + SyntheticInputAgent.COMPLETION_GRACE_SEC}s (is the simulation paused?).");

            bool aimed = aim.HasTarget;

            // Without an aim a failure is an error. With an aim it is a legitimate outcome, like a click that is
            // occluded or out of range, and goes into the result.
            if (result.FailureReason != null && !result.Hit && !aimed)
                return McpToolResult.Error($"press_input was not delivered: {result.FailureReason}");

            var json = new JObject
            {
                ["entityBound"] = result.Hit,
            };

            if (result.Hit)
            {
                json["entityId"] = result.SceneEntityId;
                json["crdtEntityId"] = result.CrdtEntityId;
                json["hitPoint"] = result.HitPoint.ToVector();
                json["distance"] = Math.Round(result.Distance, 2);

                if (result.HoverText != null)
                    json["hoverText"] = result.HoverText;
            }

            else if (!aimed)
                json["hint"] = "delivered to the scene root; pass entityId or x/y/z to aim the reticle and land it entity-bound";

            if (result.FailureReason != null)
                json[aimed && !result.Hit ? "reason" : "warning"] = result.FailureReason;

            if (result.BlockedByEntityId.HasValue)
            {
                json["blockedByEntityId"] = result.BlockedByEntityId.Value;
                json["blockedByCrdtId"] = result.BlockedByCrdtId;
                json["blockedByCollider"] = result.BlockedByColliderName;
            }

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
                SdkAction.MODIFIER => InputAction.IaModifier,
                _ => InputAction.IaPointer,
            };
    }
}
