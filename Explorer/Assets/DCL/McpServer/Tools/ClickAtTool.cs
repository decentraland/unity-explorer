using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.SyntheticInput;
using DCL.SyntheticInput.Components;
using DCL.SyntheticInput.UiSimulation;
using Newtonsoft.Json.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Clicks at a screen position via <see cref="SyntheticInputAgent" />: the reticle ray is built through
    ///     the given point (image coordinates, matching what a screenshot shows) and whatever scene entity it
    ///     lands on receives the press/release through the real reticle pipeline.
    /// </summary>
    public class ClickAtTool : McpTool
    {
        private const float DEFAULT_TIMEOUT_SEC = 3f;
        private const float MIN_TIMEOUT_SEC = 0.5f;
        private const float MAX_TIMEOUT_SEC = 15f;

        private readonly SyntheticInputAgent syntheticInput;

        public override string Name => "click_at";

        public override string Description =>
            "Press and release a pointer button at a screen position given as normalized image coordinates "
            + "(x right 0..1, y DOWN 0..1, origin at the top-left — the same way you read a screenshot). The ray through "
            + "that point decides the target: whatever qualified scene entity it lands on receives the click through the "
            + "real reticle pipeline, and a miss reports what blocked it. This clicks the 3D world, never UI — a point "
            + "covered by client UI or the scene's own UI fails with the cover (click those with ui_click), unless force "
            + "is set. Use click_entity when you know the entity id.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Number("x", "Normalized horizontal image coordinate, 0 (left) to 1 (right).", isRequired: true)
                  .Number("y", "Normalized vertical image coordinate, 0 (top) to 1 (bottom).", isRequired: true)
                  .String("sceneId", "Pin the click to this scene (id from get_scene_state): it fails instead of landing in another scene if the player moved.")
                  .Enum<PointerButton>("button", PointerArgs.BUTTON_DESCRIPTION)
                  .Number("timeoutSec", "Seconds to wait for delivery. Default 3, max 15.")
                  .Boolean("force", "Aim through UI covering that point instead of failing. Default false.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public ClickAtTool(SyntheticInputAgent syntheticInput)
        {
            this.syntheticInput = syntheticInput;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!arguments.TryGetFloat("x", out float x) || !arguments.TryGetFloat("y", out float y))
                return McpToolResult.Error("x and y normalized image coordinates are required." + arguments.NonNumericHint("x", "y"));

            if (x is < 0f or > 1f || y is < 0f or > 1f)
                return McpToolResult.Error("x and y must be normalized image coordinates in [0, 1].");

            if (!PointerArgs.TryGetButton(arguments, out InputAction button, out string? buttonError))
                return McpToolResult.Error(buttonError!);

            float timeoutSec = Mathf.Clamp(arguments.GetFloat("timeoutSec", DEFAULT_TIMEOUT_SEC), MIN_TIMEOUT_SEC, MAX_TIMEOUT_SEC);
            bool force = arguments.GetBool("force", false);

            var aim = PointerAim.AtScreenPoint(UiScreenGeometry.NormalizedImageToScreenPoint(new Vector2(x, y)), arguments.GetStringOrNull("sceneId"));

            SyntheticPointerResult result = await syntheticInput.ClickAsync(aim, button, timeoutSec, ct, force);

            if (result.TimedOut)
                return McpToolResult.Error($"click_at did not complete within {timeoutSec}s (is the simulation paused?).");

            return McpToolResult.Json(result.ToJson());
        }
    }
}
