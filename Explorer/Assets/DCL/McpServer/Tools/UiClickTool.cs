using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.SyntheticInput.UiSimulation;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.LowLevel;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Clicks a UI element. The default semantic path resolves the element and synthesizes its events after
    ///     an occlusion pre-check; the device path replays the click positionally through the virtual mouse for
    ///     full input-pipeline fidelity.
    /// </summary>
    public class UiClickTool : McpTool
    {
        /// <summary>
        ///     The member names are the wire contract (McpWireEnum derives "left"/"right"/"middle" from them), so
        ///     they follow the wire format rather than local enum-member casing.
        /// </summary>
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum ClickButton : byte
        {
            LEFT,
            RIGHT,
            MIDDLE,
        }

        private const float DEFAULT_TIMEOUT_SEC = 3f;
        private const float MIN_TIMEOUT_SEC = 0.5f;
        private const float MAX_TIMEOUT_SEC = 15f;

        /// <summary>Frames a device click at an SDK element is given to show up in the element's pointer-event slot.</summary>
        private const int SDK_DEVICE_OBSERVE_FRAMES = 6;

        private readonly UiAutomationServices uiAutomation;

        public override string Name => "ui_click";

        public override string Description =>
            "Click a UI element (client interface or SDK scene UI). " + UiAddressArgs.ADDRESS_SCHEMA_HINT + " The click fails "
            + "instead of clicking through a cover (pass force:true to bypass). device:true replays the click positionally "
            + "through the virtual mouse instead of synthesizing element events — use it for widgets that need real hit-testing.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            UiAddressArgs.DescribeAddress(schema)
                         .Enum<ClickButton>("button", "Mouse button. Default left.")
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
                return McpToolResult.Error(addressError!);

            if (!arguments.TryGetEnum("button", ClickButton.LEFT, out ClickButton button))
                return McpToolResult.Error("button must be one of: left, right, middle.");

            bool force = arguments.GetBool("force", false);
            bool device = arguments.GetBool("device", false);
            float timeoutSec = Mathf.Clamp(arguments.GetFloat("timeoutSec", DEFAULT_TIMEOUT_SEC), MIN_TIMEOUT_SEC, MAX_TIMEOUT_SEC);

            UiActionResult result;

            if (address.Stack == UiStack.SDK)
            {
                if (!uiAutomation.SdkResolver.TryResolve(address.CrdtId, out SdkUiElement element, out string? failure))
                    return McpToolResult.Error(failure!);

                result = device
                    ? await RunDeviceClickOnSdkAsync(element, button, timeoutSec, ct)
                    : await uiAutomation.Simulator.ClickSdkAsync(element, force, ct);
            }
            else
            {
                if (!uiAutomation.Discovery.TryResolve(in address, out GameObject? target, out string? failure))
                    return McpToolResult.Error(failure!);

                var rectTransform = (RectTransform)target!.transform;

                result = device
                    ? await RunDeviceClickAsync(UiScreenGeometry.ScreenCenterOf(rectTransform), UiScreenGeometry.ImageRectOf(rectTransform), button, timeoutSec, ct)
                    : uiAutomation.Simulator.ClickUgui(target, ToInputButton(button), force);
            }

            return McpToolResult.Json(result.ToJson(uiAutomation.CursorStateName()));
        }

        /// <summary>
        ///     Replays a device click at an SDK element and then reports whether the element actually observed it.
        ///     The gesture succeeding only means the device states were injected; UI Toolkit panels consume events
        ///     sent to their elements, so a driver must be told when the injected pointer never arrived instead of
        ///     reading a bare "ok" as a delivered click.
        /// </summary>
        private async UniTask<UiActionResult> RunDeviceClickOnSdkAsync(SdkUiElement element, ClickButton button, float timeoutSec, CancellationToken ct)
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

        /// <summary>
        ///     Replays the click through the virtual mouse. The element's rect travels with the result: the device
        ///     path resolved the very same element the semantic path does, so hiding its coordinates would make the
        ///     two paths answer differently about where the click landed.
        /// </summary>
        private async UniTask<UiActionResult> RunDeviceClickAsync(Vector2 screenCenter, Rect imageRect, ClickButton button, float timeoutSec, CancellationToken ct)
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

        private static Rect SdkImageRect(in SdkUiElement element) =>
            UiScreenGeometry.PanelRectToImageRect(element.Transform.Transform.panel, element.Transform.Transform.worldBound);

        private static PointerEventData.InputButton ToInputButton(ClickButton button) =>
            button switch
            {
                ClickButton.RIGHT => PointerEventData.InputButton.Right,
                ClickButton.MIDDLE => PointerEventData.InputButton.Middle,
                _ => PointerEventData.InputButton.Left,
            };

        private static MouseButton ToMouseButton(ClickButton button) =>
            button switch
            {
                ClickButton.RIGHT => MouseButton.Right,
                ClickButton.MIDDLE => MouseButton.Middle,
                _ => MouseButton.Left,
            };
    }
}
