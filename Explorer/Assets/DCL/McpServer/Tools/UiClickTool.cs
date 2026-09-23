using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.SyntheticInput.UiSimulation;
using Newtonsoft.Json.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.LowLevel;
using VisualElement = UnityEngine.UIElements.VisualElement;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Clicks a UI element by synthesizing its events (semantic path) or by replaying the virtual mouse at
    ///     its position (device path).
    /// </summary>
    public class UiClickTool : McpTool
    {
        private const int SDK_DEVICE_OBSERVE_FRAMES = 6;

        private readonly UiAutomationServices uiAutomation;

        public override string Name => "ui_click";

        public override string Description =>
            "Click a UI element (client interface or SDK scene UI). " + UiAddressArgs.ADDRESS_SCHEMA_HINT + " The click fails "
            + "instead of clicking through a cover (pass force:true to bypass). device:true replays the click positionally "
            + "through the virtual mouse instead of synthesizing element events — use it for widgets that need real hit-testing.";

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

            if (address.Stack == UiStack.SDK)
            {
                if (!uiAutomation.SdkResolver.TryResolve(address.CrdtId, out SdkUiElement element, out string? failure))
                    return McpToolResult.Error(failure);

                result = device
                    ? await RunDeviceClickOnSdkAsync(element, button, timeoutSec, ct)
                    : await uiAutomation.Simulator.ClickSdkAsync(element, force, ct);
            }
            else
            {
                if (!uiAutomation.Discovery.TryResolve(in address, out GameObject? target, out string? failure))
                    return McpToolResult.Error(failure);

                var rectTransform = (RectTransform)target.transform;

                result = device
                    ? await RunDeviceClickAsync(UiScreenGeometry.ScreenCenterOf(rectTransform), UiScreenGeometry.ImageRectOf(rectTransform), button, timeoutSec, ct)
                    : uiAutomation.Simulator.ClickUgui(target, button, force);
            }

            return McpToolResult.Json(result.ToJson(uiAutomation.CursorStateName()));
        }

        /// <summary>
        ///     A successful gesture only means the device states were injected, so the element is watched for
        ///     the pointer event it should have observed.
        /// </summary>
        private async UniTask<UiActionResult> RunDeviceClickOnSdkAsync(SdkUiElement element, PointerEventData.InputButton button, float timeoutSec, CancellationToken ct)
        {
            Rect imageRect = SdkImageRect(element);
            UiActionResult result = await RunDeviceClickAsync(UiScreenGeometry.ImageToScreenPoint(imageRect.center), imageRect, button, timeoutSec, ct);

            if (!result.Ok)
                return result;

            bool observed = element.Transform.PointerEventTriggered != null;

            for (var frame = 0; !observed && frame < SDK_DEVICE_OBSERVE_FRAMES; frame++)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                observed = element.Transform.PointerEventTriggered != null;
            }

            result.Info = observed
                ? "the element observed the device pointer event"
                : $"the element observed no pointer event within {SDK_DEVICE_OBSERVE_FRAMES} frames: the virtual device drives the client UI stack, not UI Toolkit scene panels — use the semantic path (device:false) for scene UI";

            return result;
        }

        private async UniTask<UiActionResult> RunDeviceClickAsync(Vector2 screenCenter, Rect imageRect, PointerEventData.InputButton button, float timeoutSec, CancellationToken ct)
        {
            UiGestureResult gesture = await uiAutomation.RunGestureAsync(new UiDeviceGestureRequest
            {
                Kind = UiDeviceGestureKind.Click,
                To = screenCenter,
                Button = ToMouseButton(button),
            }, timeoutSec, ct);

            return gesture.Ok
                ? UiActionResult.Success(imageRect)
                : UiActionResult.Failure(gesture.FailureReason ?? "the device click failed", null, imageRect);
        }

        private static Rect SdkImageRect(in SdkUiElement element)
        {
            VisualElement visual = element.Transform.Transform;
            return UiScreenGeometry.PanelRectToImageRect(visual.panel, visual.worldBound);
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
