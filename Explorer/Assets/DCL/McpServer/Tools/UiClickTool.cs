using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.SyntheticInput.UiSimulation;
using Newtonsoft.Json.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.LowLevel;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Clicks a UI element by synthesizing its events (semantic path) or by replaying the virtual mouse at
    ///     its position (device path).
    /// </summary>
    public class UiClickTool : McpTool
    {
        private readonly UiAutomationServices uiAutomation;

        public override string Name => "ui_click";

        public override string Description =>
            "Click a UI element (client interface or SDK scene UI). " + UiAddressArgs.ADDRESS_SCHEMA_HINT + " The click fails "
            + "instead of clicking through a cover (pass force:true to bypass). device:true replays the click positionally "
            + "through the virtual mouse instead of synthesizing element events — use it for widgets that need real hit-testing; "
            + "on scene UI it is paced one event per scene drain, and force cannot aim it through a client cover. For scene UI "
            + "the result's delivered {enter, down, up} names what the scene consumed (consumed | not_declared | dropped | "
            + "not_drained) and ok is false when a press or release the element declares was not consumed.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            UiAddressArgs.DescribeAddress(schema)
                         .Enum<PointerEventData.InputButton>("button", "Mouse button. Default left.")
                         .Boolean("force", "Skip the occlusion pre-check and click even when covered. Default false.")
                         .Boolean("device", "Replay the click through the virtual mouse at the element's center. Default false (semantic events).")
                         .Number("timeoutSec", "Seconds to wait for multi-frame clicks. Default 3, max 15.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public UiClickTool(UiAutomationServices uiAutomation)
        {
            this.uiAutomation = uiAutomation;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!UiAddressArgs.TryParse(arguments, out UiElementAddress address, out string? addressError))
                return McpToolResult.Error(addressError);

            if (!arguments.TryGetEnum("button", PointerEventData.InputButton.Left, out PointerEventData.InputButton button))
                return McpToolResult.Error(arguments.EnumArgumentError<PointerEventData.InputButton>("button"));

            bool force = arguments.GetBool("force", false);
            bool device = arguments.GetBool("device", false);
            float timeoutSec = PointerArgs.ClampTimeout(arguments);

            UiActionResult result;
            string? endedHover;

            if (address.Stack == UiStack.SDK)
            {
                if (!uiAutomation.SdkResolver.TryResolve(address.CrdtId, out SdkUiElement element, out string? failure))
                    return McpToolResult.Error(failure);

                endedHover = uiAutomation.EndHover();

                result = device
                    ? await uiAutomation.ClickSdkWithDevicesAsync(element, ToMouseButton(button), force, timeoutSec, ct)
                    : await uiAutomation.Simulator.ClickSdkAsync(element, force, ct);
            }
            else
            {
                if (!uiAutomation.Discovery.TryResolve(in address, out GameObject? target, out string? failure))
                    return McpToolResult.Error(failure);

                var rectTransform = (RectTransform)target.transform;

                endedHover = uiAutomation.EndHover();

                result = device
                    ? await uiAutomation.ClickWithDevicesAsync(UiScreenGeometry.ScreenCenterOf(rectTransform), UiScreenGeometry.ImageRectOf(rectTransform), ToMouseButton(button), timeoutSec, ct)
                    : uiAutomation.Simulator.ClickUgui(target, button, force);
            }

            result.EndedHover = endedHover;

            return McpToolResult.Json(result.ToJson(uiAutomation.CursorStateName()));
        }

        private static MouseButton ToMouseButton(PointerEventData.InputButton button) =>
            button switch
            {
                PointerEventData.InputButton.Right => MouseButton.Right,
                PointerEventData.InputButton.Middle => MouseButton.Middle,
                _ => MouseButton.Left,
            };
    }
}
