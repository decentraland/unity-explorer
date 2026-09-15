using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.SyntheticInput.UiSimulation;
using Newtonsoft.Json.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>Scrolls a UI scroll container semantically (uGUI scroll event / SDK scroll-offset).</summary>
    public class UiScrollTool : McpTool
    {
        private readonly UiAutomationServices uiAutomation;

        public override string Name => "ui_scroll";

        public override string Description =>
            "Scroll a UI scroll container by a delta. " + UiAddressArgs.ADDRESS_SCHEMA_HINT
            + " The delta follows image coordinates like every other position in these tools: POSITIVE dy scrolls the "
            + "content DOWN (toward later rows), negative dy scrolls up. Units are panel pixels for sdk and wheel "
            + "notches for ugui. The result reports the offset actually achieved, so a delta that only hits the clamp "
            + "is visible instead of looking like a no-op success.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            UiAddressArgs.DescribeAddress(schema)
                         .Number("dx", "Horizontal scroll delta. Default 0.")
                         .Number("dy", "Vertical scroll delta: positive scrolls the content down. Default 0.")
                         .Boolean("force", "Skip the occlusion pre-check (ugui only). Default false.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public UiScrollTool(UiAutomationServices uiAutomation)
        {
            this.uiAutomation = uiAutomation;
        }

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!UiAddressArgs.TryParse(arguments, out UiElementAddress address, out string? addressError))
                return UniTask.FromResult(McpToolResult.Error(addressError!));

            var delta = new Vector2(arguments.GetFloat("dx", 0f), arguments.GetFloat("dy", 0f));

            if (delta == Vector2.zero)
                return UniTask.FromResult(McpToolResult.Error("dx and dy must not both be zero."));

            UiActionResult result;

            if (address.Stack == UiStack.SDK)
            {
                if (!uiAutomation.SdkResolver.TryResolve(address.CrdtId, out SdkUiElement element, out string? failure))
                    return UniTask.FromResult(McpToolResult.Error(failure!));

                result = uiAutomation.Simulator.ScrollSdk(element, delta);
            }
            else
            {
                if (!uiAutomation.Discovery.TryResolve(in address, out GameObject? target, out string? failure))
                    return UniTask.FromResult(McpToolResult.Error(failure!));

                result = uiAutomation.Simulator.ScrollUgui(target!, delta, arguments.GetBool("force", false));
            }

            return UniTask.FromResult(McpToolResult.Json(result.ToJson(uiAutomation.CursorStateName())));
        }
    }
}
