using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.SyntheticInput.UiSimulation;
using Newtonsoft.Json.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Parks a hover on a UI element until it is ended, by synthesizing its enter events (semantic path) or by
    ///     resting the virtual mouse on it (device path).
    /// </summary>
    public class UiHoverTool : McpTool
    {
        private const string WORLD = "world";

        private static readonly string[] ADDRESS_ARGUMENTS = { "stack", "path", "id", "altId", "crdtId" };

        private readonly UiAutomationServices uiAutomation;

        public override string Name => "ui_hover";

        public override string Description =>
            "Hover a UI element (client interface or SDK scene UI) and leave the pointer resting on it, so its hover "
            + "state can be screenshotted. " + UiAddressArgs.ADDRESS_SCHEMA_HINT + " The hover lasts until ui_hover "
            + "leave:true, the next ui_hover, or any other ui_* gesture ends it (each reports hoverEnded). Fails instead "
            + "of hovering through a cover (pass force:true to bypass). device:true rests the virtual mouse on a client "
            + "element instead of synthesizing its events.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            UiAddressArgs.DescribeAddress(schema)
                         .Boolean("leave", "End the parked hover instead of starting one; takes no address. Default false.")
                         .Boolean("force", "Skip the occlusion pre-check and hover even when covered. Default false.")
                         .Boolean("device", "Rest the virtual mouse on the element's center (client UI only). Default false (semantic events).")
                         .Number("timeoutSec", "Seconds to wait for the hover to be delivered. Default 3, max 15.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public UiHoverTool(UiAutomationServices uiAutomation)
        {
            this.uiAutomation = uiAutomation;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (arguments.GetBool("leave", false))
                return Leave(arguments);

            if (!UiAddressArgs.TryParse(arguments, out UiElementAddress address, out string? addressError))
                return McpToolResult.Error(addressError);

            bool force = arguments.GetBool("force", false);
            bool device = arguments.GetBool("device", false);
            float timeoutSec = PointerArgs.ClampTimeout(arguments);

            if (address.Stack == UiStack.SDK)
            {
                if (device)
                    return McpToolResult.Error("device:true applies only to client UI: the virtual mouse does not reach the scene's UI panels. Omit device to hover scene UI.");

                if (!uiAutomation.SdkResolver.TryResolve(address.CrdtId, out SdkUiElement element, out string? sdkFailure))
                    return McpToolResult.Error(sdkFailure);

                UiActionResult sdkResult = await uiAutomation.HoverSdkAsync(address, element, force, ct);
                return McpToolResult.Json(HoverJson(sdkResult));
            }

            if (!uiAutomation.Discovery.TryResolve(in address, out GameObject? target, out string? failure))
                return McpToolResult.Error(failure);

            UiHoverOutcome outcome = await uiAutomation.HoverUguiAsync(address, target, force, device, timeoutSec, ct);
            JObject json = HoverJson(outcome.Result);

            if (outcome.ReportsPointerOver)
                json["pointerOver"] = outcome.PointerOver ?? WORLD;

            return McpToolResult.Json(json);
        }

        private McpToolResult Leave(JObject arguments)
        {
            foreach (string addressArgument in ADDRESS_ARGUMENTS)
                if (arguments.ContainsKey(addressArgument))
                    return McpToolResult.Error($"leave:true ends whichever hover is parked and takes no address; drop '{addressArgument}'.");

            string? left = uiAutomation.EndHover();

            var json = new JObject
            {
                ["ok"] = true,
                ["left"] = left,
            };

            if (left == null)
                json["info"] = "no hover was parked, so nothing was left";

            return McpToolResult.Json(json);
        }

        private JObject HoverJson(UiActionResult result)
        {
            JObject json = result.ToJson(uiAutomation.CursorStateName());

            if (uiAutomation.HoveredAddress is { } hovered)
                json["hovering"] = hovered;

            return json;
        }
    }
}
