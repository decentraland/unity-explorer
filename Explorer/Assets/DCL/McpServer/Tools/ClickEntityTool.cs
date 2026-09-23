using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.SyntheticInput;
using DCL.SyntheticInput.Components;
using Newtonsoft.Json.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Presses a pointer button on a scene entity through the real reticle pipeline, so occlusion, distance
    ///     gates and the scene write-back are the production ones.
    /// </summary>
    public class ClickEntityTool : McpTool
    {
        /// <summary>Wire-facing gesture kinds: a full click, or a single press/release leg.</summary>
        private enum ClickKind : byte
        {
            /// <summary>Pointer down, then pointer up on the next scene tick.</summary>
            Click,
            Down,
            Up,
        }

        private readonly SyntheticInputAgent syntheticInput;

        public override string Name => "click_entity";

        public override string Description =>
            "Press and release a pointer button on a scene entity so its PointerEvents fire exactly like a real click. "
            + "The click runs through the real reticle pipeline: occluders and the entity's maxDistance apply, and a miss "
            + "returns hit:false with the blocking entity. When you passed an entityId the button is delivered to that "
            + "entity or to nobody: not to the blocker, and not to the scene root either (the blocker still sees hover "
            + "enter/leave, as a real cursor passing over it would). An aim given only as x/y/z names no entity, so a "
            + "refusal there still broadcasts the press to the scene root, like any unqualified edge. Ids "
            + "come from list_scene_entities and are engine entity ids, NOT the CRDT ids a scene logs; the result's "
            + "crdtEntityId is the CRDT one, so compare it against the entity you meant. For entities whose collider "
            + "sits away from their pivot (e.g. GLTF meshes), pass an explicit x/y/z world point to aim at.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            PointerArgs.DescribeAim(schema, "click")
                       .Enum<PointerButton>("button", PointerArgs.BUTTON_DESCRIPTION)
                  .Enum<ClickKind>("eventType", "click = down, then up on the next scene tick. Default click.")
                  .Number("timeoutSec", "Seconds to wait for delivery. Default 3, max 15.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public ClickEntityTool(SyntheticInputAgent syntheticInput)
        {
            this.syntheticInput = syntheticInput;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!PointerArgs.TryParseAim(arguments, requireTarget: true, out PointerAim aim, out string? aimError))
                return McpToolResult.Error(aimError);

            if (!PointerArgs.TryGetButton(arguments, out InputAction button, out string? buttonError))
                return McpToolResult.Error(buttonError);

            if (!arguments.TryGetEnum("eventType", ClickKind.Click, out ClickKind kind))
                return McpToolResult.Error(arguments.EnumArgumentError<ClickKind>("eventType"));

            float timeoutSec = PointerArgs.ClampTimeout(arguments);

            SyntheticPointerResult result = kind switch
                                            {
                                                ClickKind.Down => await syntheticInput.PointerDownAsync(aim, button, timeoutSec, ct: ct),
                                                ClickKind.Up => await syntheticInput.PointerUpAsync(aim, button, timeoutSec, ct: ct),
                                                _ => await syntheticInput.ClickAsync(aim, button, timeoutSec, ct: ct),
                                            };

            if (result.TimedOut)
                return McpToolResult.Error($"click_entity did not complete within {timeoutSec}s (is the simulation paused?).");

            return McpToolResult.Json(result.ToJson());
        }
    }
}
