#if ALTTESTER
using Cysharp.Threading.Tasks;
using DCL.SyntheticInput.UiSimulation;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.LowLevel;

namespace DCL.SyntheticInput.AltTester
{
    /// <summary>
    ///     AltTester front-end of the UI simulation. The class, method and assembly (<c>DCL.SyntheticInput</c>)
    ///     names are a wire contract: AltTester resolves them by string.
    /// </summary>
    public static class UiAutomationProbe
    {
        private const float SDK_CLICK_TIMEOUT_SEC = 5f;
        private const float GESTURE_TIMEOUT_GRACE_SEC = 5f;

        private const string WORLD = "world";

        private static UiAutomationServices? services;

        public static void Install(UiAutomationServices installedServices) =>
            services = installedServices;

        /// <summary>Withdraws the services before they are disposed, so no probe call reaches a disposed session.</summary>
        public static void Uninstall() =>
            services = null;

        public static bool IsReady() =>
            services != null;

        public static string PollJson(int operationId) =>
            AltOperationRegistry.PollJson(operationId);

        public static string ListInteractableJson(string stack, bool checkOcclusion)
        {
            if (!TryGetServices(out UiAutomationServices? ready, out string? failedPayload))
                return failedPayload;

            var payload = new JObject { ["ok"] = true };
            payload.Merge(ready.ListInteractableJson(stack != "sdk", stack != "ugui", checkOcclusion));

            return payload.ToString();
        }

        public static string ClickJson(string addressKind, string addressValue, string button, bool force)
        {
            if (!TryGetServices(out UiAutomationServices? ready, out string? failedPayload))
                return failedPayload;

            if (!TryResolveUgui(ready, addressKind, addressValue, out GameObject? target, out string? resolveFailure))
                return resolveFailure;

            if (!Enum.TryParse(button, ignoreCase: true, out PointerEventData.InputButton inputButton) || !Enum.IsDefined(typeof(PointerEventData.InputButton), inputButton))
                return AltOperationRegistry.ErrorPayload($"unknown button '{button}' (use left, right or middle)");

            string? endedHover = ready.EndHover();
            UiActionResult result = ready.Simulator.ClickUgui(target, inputButton, force);
            result.EndedHover = endedHover;
            return result.ToJson(ready.CursorStateName()).ToString();
        }

        public static string SetTextJson(string addressKind, string addressValue, string text, bool submit)
        {
            if (!TryGetServices(out UiAutomationServices? ready, out string? failedPayload))
                return failedPayload;

            if (!TryResolveUgui(ready, addressKind, addressValue, out GameObject? target, out string? resolveFailure))
                return resolveFailure;

            string? endedHover = ready.EndHover();
            UiActionResult result = ready.Simulator.SetTextUgui(target, text, submit);
            result.EndedHover = endedHover;
            return result.ToJson(ready.CursorStateName()).ToString();
        }

        public static string ScrollJson(string addressKind, string addressValue, float dx, float dy, bool force)
        {
            if (!TryGetServices(out UiAutomationServices? ready, out string? failedPayload))
                return failedPayload;

            if (!TryResolveUgui(ready, addressKind, addressValue, out GameObject? target, out string? resolveFailure))
                return resolveFailure;

            string? endedHover = ready.EndHover();
            UiActionResult result = ready.Simulator.ScrollUgui(target, new Vector2(dx, dy), force);
            result.EndedHover = endedHover;
            return result.ToJson(ready.CursorStateName()).ToString();
        }

        public static string SetTextSdkJson(int crdtId, string text, bool submit)
        {
            if (!TryGetServices(out UiAutomationServices? ready, out string? failedPayload))
                return failedPayload;

            if (!ready.SdkResolver.TryResolve(crdtId, out SdkUiElement element, out string? failure))
                return AltOperationRegistry.ErrorPayload(failure);

            string? endedHover = ready.EndHover();
            UiActionResult result = ready.Simulator.SetTextSdk(element, text, submit);
            result.EndedHover = endedHover;
            return result.ToJson(ready.CursorStateName()).ToString();
        }

        public static string SelectDropdownSdkJson(int crdtId, int optionIndex)
        {
            if (!TryGetServices(out UiAutomationServices? ready, out string? failedPayload))
                return failedPayload;

            if (!ready.SdkResolver.TryResolve(crdtId, out SdkUiElement element, out string? failure))
                return AltOperationRegistry.ErrorPayload(failure);

            string? endedHover = ready.EndHover();
            UiActionResult result = ready.Simulator.SelectDropdownSdk(element, optionIndex);
            result.EndedHover = endedHover;
            return result.ToJson(ready.CursorStateName()).ToString();
        }

        public static string ScrollSdkJson(int crdtId, float dx, float dy)
        {
            if (!TryGetServices(out UiAutomationServices? ready, out string? failedPayload))
                return failedPayload;

            if (!ready.SdkResolver.TryResolve(crdtId, out SdkUiElement element, out string? failure))
                return AltOperationRegistry.ErrorPayload(failure);

            string? endedHover = ready.EndHover();
            UiActionResult result = ready.Simulator.ScrollSdk(element, new Vector2(dx, dy));
            result.EndedHover = endedHover;
            return result.ToJson(ready.CursorStateName()).ToString();
        }

        public static int StartSdkClick(int crdtId, bool force)
        {
            if (services == null)
                return AltOperationRegistry.Start(UniTask.FromResult(NotInstalledPayload()));

            UiAutomationServices ready = services;

            if (!ready.SdkResolver.TryResolve(crdtId, out SdkUiElement element, out string? failure))
                return AltOperationRegistry.Start(UniTask.FromResult(AltOperationRegistry.ErrorPayload(failure)));

            string? endedHover = ready.EndHover();

            // The timeout cancels the click as well, so an expired click does not keep driving the scene UI.
            var clickCancellation = new CancellationTokenSource();

            return AltOperationRegistry.Start(
                ready.Simulator.ClickSdkAsync(element, force, clickCancellation.Token)
                     .Timeout(TimeSpan.FromSeconds(SDK_CLICK_TIMEOUT_SEC), taskCancellationTokenSource: clickCancellation)
                     .ContinueWith(result => WithEndedHover(result, endedHover).ToJson(ready.CursorStateName()).ToString()));
        }

        /// <summary>
        ///     Clicks a scene-UI element through the virtual mouse (start/poll), paced one input state per scene drain.
        ///     The payload's <c>delivered</c> names which events the scene consumed.
        /// </summary>
        public static int StartSdkDeviceClick(int crdtId, bool rightButton, bool force)
        {
            if (services == null)
                return AltOperationRegistry.Start(UniTask.FromResult(NotInstalledPayload()));

            UiAutomationServices ready = services;

            if (!ready.SdkResolver.TryResolve(crdtId, out SdkUiElement element, out string? failure))
                return AltOperationRegistry.Start(UniTask.FromResult(AltOperationRegistry.ErrorPayload(failure)));

            string? endedHover = ready.EndHover();

            return AltOperationRegistry.Start(
                ready.ClickSdkWithDevicesAsync(element, rightButton ? MouseButton.Right : MouseButton.Left, force, SDK_CLICK_TIMEOUT_SEC, CancellationToken.None)
                     .ContinueWith(result => WithEndedHover(result, endedHover).ToJson(ready.CursorStateName()).ToString()));
        }

        public static int StartDrag(float fromX, float fromY, float toX, float toY, int durationFrames, bool rightButton)
        {
            if (services == null)
                return AltOperationRegistry.Start(UniTask.FromResult(NotInstalledPayload()));

            UiAutomationServices ready = services;
            int frames = Mathf.Clamp(durationFrames, 2, 300);

            Vector2 from = UiScreenGeometry.NormalizedImageToScreenPoint(new Vector2(fromX, fromY));
            Vector2 to = UiScreenGeometry.NormalizedImageToScreenPoint(new Vector2(toX, toY));
            string? endedHover = ready.EndHover();

            return AltOperationRegistry.Start(
                ready.DragWithDevicesAsync(from, to, frames, rightButton ? MouseButton.Right : MouseButton.Left, CancellationToken.None)
                     .ContinueWith(outcome => DragPayload(outcome, endedHover)));
        }

        public static int StartDeviceClick(float x, float y, bool rightButton)
        {
            if (services == null)
                return AltOperationRegistry.Start(UniTask.FromResult(NotInstalledPayload()));

            UiAutomationServices ready = services;

            var request = new UiDeviceGestureRequest
            {
                Kind = UiDeviceGestureKind.Click,
                To = UiScreenGeometry.NormalizedImageToScreenPoint(new Vector2(x, y)),
                Button = rightButton ? MouseButton.Right : MouseButton.Left,
            };

            string? endedHover = ready.EndHover();

            return AltOperationRegistry.Start(ready.RunGestureAsync(request, GESTURE_TIMEOUT_GRACE_SEC, CancellationToken.None)
                                                   .ContinueWith(result => GesturePayload(result, endedHover)));
        }

        /// <summary>
        ///     Parks a hover on a client UI element (start/poll): synthesized enter events, or with
        ///     <paramref name="device" /> the virtual mouse resting on its center. It lasts until <see cref="EndHoverJson" />,
        ///     the next hover, or any other gesture of this probe.
        /// </summary>
        public static int StartHover(string addressKind, string addressValue, bool force, bool device)
        {
            if (services == null)
                return AltOperationRegistry.Start(UniTask.FromResult(NotInstalledPayload()));

            UiAutomationServices ready = services;

            if (!TryResolveUgui(ready, addressKind, addressValue, out UiElementAddress address, out GameObject? target, out string? resolveFailure))
                return AltOperationRegistry.Start(UniTask.FromResult(resolveFailure));

            return AltOperationRegistry.Start(
                ready.HoverUguiAsync(address, target, force, device, GESTURE_TIMEOUT_GRACE_SEC, CancellationToken.None)
                     .ContinueWith(outcome => HoverPayload(ready, outcome.Result, outcome.ReportsPointerOver, outcome.PointerOver)));
        }

        /// <summary>Parks a hover on a scene UI element (start/poll); the enter is held until the scene consumed it.</summary>
        public static int StartSdkHover(int crdtId, bool force)
        {
            if (services == null)
                return AltOperationRegistry.Start(UniTask.FromResult(NotInstalledPayload()));

            UiAutomationServices ready = services;

            if (!ready.SdkResolver.TryResolve(crdtId, out SdkUiElement element, out string? failure))
                return AltOperationRegistry.Start(UniTask.FromResult(AltOperationRegistry.ErrorPayload(failure)));

            var hoverCancellation = new CancellationTokenSource();

            return AltOperationRegistry.Start(
                ready.HoverSdkAsync(UiElementAddress.Sdk(crdtId), element, force, hoverCancellation.Token)
                     .Timeout(TimeSpan.FromSeconds(SDK_CLICK_TIMEOUT_SEC), taskCancellationTokenSource: hoverCancellation)
                     .ContinueWith(result => HoverPayload(ready, result, false, null)));
        }

        /// <summary>Ends the parked hover; <c>left</c> is null when none was parked.</summary>
        public static string EndHoverJson()
        {
            if (!TryGetServices(out UiAutomationServices? ready, out string? failedPayload))
                return failedPayload;

            return new JObject
            {
                ["ok"] = true,
                ["left"] = ready.EndHover(),
            }.ToString();
        }

        private static bool TryGetServices([NotNullWhen(true)] out UiAutomationServices? ready, [NotNullWhen(false)] out string? failedPayload)
        {
            if (services != null)
            {
                ready = services;
                failedPayload = null;
                return true;
            }

            ready = null;
            failedPayload = NotInstalledPayload();
            return false;
        }

        private static string NotInstalledPayload() =>
            AltOperationRegistry.ErrorPayload("the synthetic input layer is not installed (launch with --alttester or --mcp)");

        private static bool TryResolveUgui(UiAutomationServices ready, string addressKind, string addressValue, [NotNullWhen(true)] out GameObject? target, [NotNullWhen(false)] out string? failedPayload) =>
            TryResolveUgui(ready, addressKind, addressValue, out _, out target, out failedPayload);

        private static bool TryResolveUgui(UiAutomationServices ready, string addressKind, string addressValue, out UiElementAddress address,
            [NotNullWhen(true)] out GameObject? target, [NotNullWhen(false)] out string? failedPayload)
        {
            target = null;
            failedPayload = null;
            address = default(UiElementAddress);

            switch (addressKind)
            {
                case "path":
                    address = UiElementAddress.UguiPath(addressValue);
                    break;
                case "altId":
                    address = UiElementAddress.UguiAltId(addressValue);
                    break;
                case "id" when ulong.TryParse(addressValue, out ulong instanceId):
                    address = UiElementAddress.UguiInstance(instanceId);
                    break;
                default:
                    failedPayload = AltOperationRegistry.ErrorPayload($"unknown address kind '{addressKind}' (use path, altId or id)");
                    return false;
            }

            if (!ready.Discovery.TryResolve(in address, out GameObject? resolved, out string? failure))
            {
                failedPayload = AltOperationRegistry.ErrorPayload(failure);
                return false;
            }

            target = resolved;
            return true;
        }

        private static UiActionResult WithEndedHover(UiActionResult result, string? endedHover)
        {
            result.EndedHover = endedHover;
            return result;
        }

        private static string HoverPayload(UiAutomationServices ready, UiActionResult result, bool reportsPointerOver, string? pointerOver)
        {
            JObject payload = result.ToJson(ready.CursorStateName());

            if (ready.HoveredAddress is { } hovered)
                payload["hovering"] = hovered;

            if (reportsPointerOver)
                payload["pointerOver"] = pointerOver ?? WORLD;

            return payload.ToString();
        }

        private static string DragPayload(UiDeviceDragOutcome outcome, string? endedHover)
        {
            var payload = new JObject
            {
                ["ok"] = outcome.Ok,
                ["pointerOverStart"] = outcome.CoverAtStart ?? WORLD,
                ["pointerOverEnd"] = outcome.CoverAtEnd ?? WORLD,
            };

            if (outcome.FailureReason != null)
                payload["error"] = outcome.FailureReason;

            if (outcome.DeliveryNote != null)
                payload["info"] = outcome.DeliveryNote;

            if (endedHover != null)
                payload["hoverEnded"] = endedHover;

            return payload.ToString();
        }

        private static string GesturePayload(UiGestureResult result, string? endedHover)
        {
            var payload = new JObject { ["ok"] = result.Ok };

            if (result.FailureReason != null)
                payload["error"] = result.FailureReason;

            if (endedHover != null)
                payload["hoverEnded"] = endedHover;

            return payload.ToString();
        }
    }
}
#endif
