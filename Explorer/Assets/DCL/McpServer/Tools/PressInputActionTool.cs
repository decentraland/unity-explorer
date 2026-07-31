using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.McpServer.Components;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Sends an <see cref="InputAction" /> edge to the current scene with no entity and no aim involved, by
    ///     way of an <see cref="McpInputActionIntent" /> that McpInputActionSystem publishes: it lands on the
    ///     scene root entity the way a key press does, which is what a scene polling inputSystem.isTriggered /
    ///     isPressed every frame reads. This is the counterpart of click_entity, whose every path requires a
    ///     collider the reticle can qualify — so a game that never registers a PointerEvents component is
    ///     unreachable through it. A press owns its release: the system emits the PetUp even if this call is
    ///     cancelled, so a dropped connection cannot leave the scene with a button stuck down.
    /// </summary>
    public class PressInputActionTool : McpTool
    {
        /// <summary>Wire-facing gesture kinds: a press that releases itself, or a single down/up leg.</summary>
        private enum PressKind : byte
        {
            /// <summary>Down, then up once <c>holdSec</c> has elapsed and the scene has advanced a tick.</summary>
            PRESS,

            /// <summary>Down-only leg; parsed from the wire and exposed via the schema through reflection over this enum.</summary>
            [UsedImplicitly]
            DOWN,
            UP,
        }

        private const float DEFAULT_HOLD_SEC = 0.2f;
        private const float MIN_HOLD_SEC = 0.1f;
        private const float MAX_HOLD_SEC = 30f;

        private const float DEFAULT_TIMEOUT_SEC = 3f;
        private const float MIN_TIMEOUT_SEC = 0.5f;
        private const float MAX_TIMEOUT_SEC = 15f;

        private static readonly string ACTION_NAMES = string.Join(", ", McpWireEnum<McpInputAction>.WIRE_NAMES);
        private static readonly string KIND_NAMES = string.Join(", ", McpWireEnum<PressKind>.WIRE_NAMES);

        private readonly World world;
        private readonly Entity playerEntity;

        public override string Name => "press_input_action";

        public override string Description =>
            "Send an SDK input action to the current scene without pointing at anything, so scenes that poll input "
            + "globally (inputSystem.isTriggered / isPressed, with no entity argument) react. Use this for game "
            + "controls; use click_entity when the scene registered the action on a specific entity. press holds "
            + "the button for holdSec and releases it on a later scene tick, so a scene reading isPressed observes "
            + "a real hold. A lone down leaves the scene believing the button is still held until a matching up.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Enum<McpInputAction>("action", "Which input action to send, e.g. primary (IA_PRIMARY) or action_5 (IA_ACTION_5).", isRequired: true)
                  .Enum<PressKind>("eventType", "press = down, then up after holdSec. Default press.")
                  .Number("holdSec", "How long a press keeps the button down. Default 0.2, min 0.1, max 30.")
                  .String("sceneId", "Pin the input to this scene (id from get_scene_state): it fails instead of landing in another scene if the player moved.")
                  .Number("timeoutSec", "Seconds to wait for the edge to be published. Default 3, max 15.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public PressInputActionTool(World world, Entity playerEntity)
        {
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!arguments.TryGetEnum("action", out McpInputAction action))
                return McpToolResult.Error($"action is required and must be one of: {ACTION_NAMES}.");

            if (!arguments.TryGetEnum("eventType", PressKind.PRESS, out PressKind kind))
                return McpToolResult.Error($"eventType must be one of: {KIND_NAMES}.");

            float holdSec = Mathf.Clamp(arguments.GetFloat("holdSec", DEFAULT_HOLD_SEC), MIN_HOLD_SEC, MAX_HOLD_SEC);
            float timeoutSec = Mathf.Clamp(arguments.GetFloat("timeoutSec", DEFAULT_TIMEOUT_SEC), MIN_TIMEOUT_SEC, MAX_TIMEOUT_SEC);
            string? sceneId = arguments["sceneId"]?.Type == JTokenType.String ? arguments["sceneId"]!.Value<string>() : null;

            var intent = new McpInputActionIntent(
                action.ToInputAction(),
                sceneId,
                kind == PressKind.UP ? PointerEventType.PetUp : PointerEventType.PetDown,
                kind == PressKind.PRESS ? holdSec : null);

            // A press spends most of its budget holding the button down, so the timeout that guards a stuck
            // simulation has to clear the hold itself before it can mean anything.
            float budgetSec = kind == PressKind.PRESS ? holdSec + timeoutSec : timeoutSec;

            McpInputActionResult result;

            try
            {
                result = await McpEcsRequest.SendAsync(world, playerEntity, intent, PreemptedResult(world, playerEntity))
                                            .AttachExternalCancellation(ct)
                                            .Timeout(TimeSpan.FromSeconds(budgetSec));
            }
            catch (TimeoutException)
            {
                await McpEcsRequest.AbandonAsync<McpInputActionIntent>(world, playerEntity);
                return McpToolResult.Error($"press_input_action did not complete within {budgetSec}s (is the simulation paused?).");
            }

            var json = new JObject
            {
                ["delivered"] = result.Delivered,
                ["action"] = McpWireEnum<McpInputAction>.ToWire(action),
                ["eventType"] = McpWireEnum<PressKind>.ToWire(kind),
            };

            if (result.SceneId != null)
                json["sceneId"] = result.SceneId;

            if (result.FailureReason != null)
                json["reason"] = result.FailureReason;

            if (kind == PressKind.PRESS && result.Delivered && !result.ReleaseMissed)
                json["heldSec"] = Math.Round(result.HeldSeconds, 2);

            if (result.ReleaseMissed)
                json["releaseMissed"] = true;

            return McpToolResult.Json(json);
        }

        /// <summary>
        ///     What the call being preempted reports. Only one input action is in flight at a time, so this call
        ///     drops whatever the previous one had going; when that was a press already down, the scene keeps
        ///     seeing the button held until the same action is sent again with eventType up, which is what
        ///     releaseMissed tells the preempted caller. It has to be read before SendAsync overwrites it.
        /// </summary>
        private static McpInputActionResult PreemptedResult(World world, Entity playerEntity)
        {
            bool heldPressDropped = world.TryGet(playerEntity, out McpInputActionIntent pending)
                                    && pending.PressTime.HasValue;

            return new McpInputActionResult
            {
                Delivered = heldPressDropped,
                ReleaseMissed = heldPressDropped,
                FailureReason = "preempted by a newer press_input_action call",
            };
        }
    }
}
