#if ALTTESTER
using Cysharp.Threading.Tasks;
using DCL.SyntheticInput.UiSimulation;
using Newtonsoft.Json.Linq;
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

        public static bool IsReady() =>
            services != null;

        public static string PollJson(int operationId) =>
            AltOperationRegistry.PollJson(operationId);

        public static string ListInteractableJson(string stack, bool checkOcclusion)
        {
            if (!TryGetServices(out UiAutomationServices ready, out string failedPayload))
                return failedPayload;

            var payload = new JObject { ["ok"] = true };
            payload.Merge(ready.ListInteractableJson(stack != "sdk", stack != "ugui", checkOcclusion));

            return payload.ToString();
        }

        public static string ClickJson(string addressKind, string addressValue, string button, bool force)
        {
            if (!TryGetServices(out UiAutomationServices ready, out string failedPayload))
                return failedPayload;

            if (!TryResolveUgui(ready, addressKind, addressValue, out GameObject target, out string resolveFailure))
                return resolveFailure;

            UiActionResult result = ready.Simulator.ClickUgui(target, ParseInputButton(button), force);
            return result.ToJson(ready.CursorStateName()).ToString();
        }

        public static string SetTextJson(string addressKind, string addressValue, string text, bool submit)
        {
            if (!TryGetServices(out UiAutomationServices ready, out string failedPayload))
                return failedPayload;

            if (!TryResolveUgui(ready, addressKind, addressValue, out GameObject target, out string resolveFailure))
                return resolveFailure;

            return ready.Simulator.SetTextUgui(target, text, submit).ToJson(ready.CursorStateName()).ToString();
        }

        public static string ScrollJson(string addressKind, string addressValue, float dx, float dy, bool force)
        {
            if (!TryGetServices(out UiAutomationServices ready, out string failedPayload))
                return failedPayload;

            if (!TryResolveUgui(ready, addressKind, addressValue, out GameObject target, out string resolveFailure))
                return resolveFailure;

            return ready.Simulator.ScrollUgui(target, new Vector2(dx, dy), force).ToJson(ready.CursorStateName()).ToString();
        }

        public static string SetTextSdkJson(int crdtId, string text, bool submit)
        {
            if (!TryGetServices(out UiAutomationServices ready, out string failedPayload))
                return failedPayload;

            if (!ready.SdkResolver.TryResolve(crdtId, out SdkUiElement element, out string? failure))
                return AltOperationRegistry.ErrorPayload(failure!);

            return ready.Simulator.SetTextSdk(element, text, submit).ToJson(ready.CursorStateName()).ToString();
        }

        public static string SelectDropdownSdkJson(int crdtId, int optionIndex)
        {
            if (!TryGetServices(out UiAutomationServices ready, out string failedPayload))
                return failedPayload;

            if (!ready.SdkResolver.TryResolve(crdtId, out SdkUiElement element, out string? failure))
                return AltOperationRegistry.ErrorPayload(failure!);

            return ready.Simulator.SelectDropdownSdk(element, optionIndex).ToJson(ready.CursorStateName()).ToString();
        }

        public static string ScrollSdkJson(int crdtId, float dx, float dy)
        {
            if (!TryGetServices(out UiAutomationServices ready, out string failedPayload))
                return failedPayload;

            if (!ready.SdkResolver.TryResolve(crdtId, out SdkUiElement element, out string? failure))
                return AltOperationRegistry.ErrorPayload(failure!);

            return ready.Simulator.ScrollSdk(element, new Vector2(dx, dy)).ToJson(ready.CursorStateName()).ToString();
        }

        public static int StartSdkClick(int crdtId, bool force)
        {
            if (services == null)
                return AltOperationRegistry.Start(UniTask.FromResult(NotInstalledPayload()));

            UiAutomationServices ready = services;

            if (!ready.SdkResolver.TryResolve(crdtId, out SdkUiElement element, out string? failure))
                return AltOperationRegistry.Start(UniTask.FromResult(AltOperationRegistry.ErrorPayload(failure!)));

            return AltOperationRegistry.Start(
                ready.Simulator.ClickSdkAsync(element, force, CancellationToken.None)
                     .Timeout(System.TimeSpan.FromSeconds(SDK_CLICK_TIMEOUT_SEC))
                     .ContinueWith(result => result.ToJson(ready.CursorStateName()).ToString()));
        }

        public static int StartDrag(float fromX, float fromY, float toX, float toY, int durationFrames, bool rightButton)
        {
            if (services == null)
                return AltOperationRegistry.Start(UniTask.FromResult(NotInstalledPayload()));

            UiAutomationServices ready = services;
            int frames = Mathf.Clamp(durationFrames, 2, 300);

            Vector2 from = UiScreenGeometry.NormalizedImageToScreenPoint(new Vector2(fromX, fromY));
            Vector2 to = UiScreenGeometry.NormalizedImageToScreenPoint(new Vector2(toX, toY));

            return AltOperationRegistry.Start(
                ready.DragWithDevicesAsync(from, to, frames, rightButton ? MouseButton.Right : MouseButton.Left, CancellationToken.None)
                     .ContinueWith(DragPayload));
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

            return AltOperationRegistry.Start(ready.RunGestureAsync(request, GESTURE_TIMEOUT_GRACE_SEC, CancellationToken.None).ContinueWith(GesturePayload));
        }

        private static bool TryGetServices(out UiAutomationServices ready, out string failedPayload)
        {
            if (services != null)
            {
                ready = services;
                failedPayload = string.Empty;
                return true;
            }

            ready = null!;
            failedPayload = NotInstalledPayload();
            return false;
        }

        private static string NotInstalledPayload() =>
            AltOperationRegistry.ErrorPayload("the synthetic input layer is not installed (launch with --alttester or --mcp)");

        private static bool TryResolveUgui(UiAutomationServices ready, string addressKind, string addressValue, out GameObject target, out string failedPayload)
        {
            target = null!;
            failedPayload = string.Empty;

            UiElementAddress address;

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
                failedPayload = AltOperationRegistry.ErrorPayload(failure!);
                return false;
            }

            target = resolved!;
            return true;
        }

        private static PointerEventData.InputButton ParseInputButton(string button) =>
            button switch
            {
                "right" => PointerEventData.InputButton.Right,
                "middle" => PointerEventData.InputButton.Middle,
                _ => PointerEventData.InputButton.Left,
            };

        private static string DragPayload(UiDeviceDragOutcome outcome)
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

            return payload.ToString();
        }

        private static string GesturePayload(UiGestureResult result)
        {
            var payload = new JObject { ["ok"] = result.Ok };

            if (result.FailureReason != null)
                payload["error"] = result.FailureReason;

            return payload.ToString();
        }
    }
}
#endif
