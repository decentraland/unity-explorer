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
    ///     <para>
    ///         AltTester front-end of the UI simulation: tests call these via <c>AltDriver.CallStaticMethod</c>
    ///         (assembly <c>DCL.SyntheticInput</c> — this assembly name is a wire contract) and drive the same
    ///         <see cref="UiAutomationServices" /> the MCP ui_* tools drive.
    ///     </para>
    ///     <para>
    ///         Synchronous semantic actions (uGUI click/text/scroll, SDK text/dropdown/scroll, listing) return
    ///         their payload in one round-trip; multi-frame ones (SDK clicks, device gestures) use start/poll via
    ///         <see cref="PollJson" />. uGUI addressing: addressKind ∈ path|altId|id with the matching value.
    ///     </para>
    /// </summary>
    public static class UiAutomationProbe
    {
        private const float SDK_CLICK_TIMEOUT_SEC = 5f;
        private const float GESTURE_TIMEOUT_GRACE_SEC = 5f;
        private const float ASSUMED_MIN_FPS = 15f;

        private static UiAutomationServices? services;

        /// <summary>Written once by SyntheticInputPlugin when the automation session starts (the static-latch probe pattern).</summary>
        public static void Install(UiAutomationServices installedServices) =>
            services = installedServices;

        public static bool IsReady() =>
            services != null;

        public static string PollJson(int operationId) =>
            AltOperationRegistry.PollJson(operationId);

        /// <summary>Lists interactable UI; stack ∈ all|ugui|sdk. screenRect is in image pixels (origin top-left).</summary>
        public static string ListInteractableJson(string stack, bool checkOcclusion)
        {
            if (!TryGetServices(out UiAutomationServices ready, out string failedPayload))
                return failedPayload;

            var elements = new JArray();

            if (stack != "sdk")
                foreach (JToken entry in ready.Discovery.ListInteractable(checkOcclusion))
                    elements.Add(entry);

            if (stack != "ugui")
                foreach (JToken entry in ready.SdkResolver.ListInteractable())
                    elements.Add(entry);

            return new JObject { ["ok"] = true, ["count"] = elements.Count, ["elements"] = elements }.ToString();
        }

        /// <summary>Semantic uGUI click; button ∈ left|right|middle.</summary>
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

        /// <summary>SDK scene-UI click: two-frame, so start/poll.</summary>
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

        /// <summary>Virtual-mouse drag between two normalized image points (x right 0..1, y DOWN 0..1, origin top-left).</summary>
        public static int StartDrag(float fromX, float fromY, float toX, float toY, int durationFrames, bool rightButton)
        {
            if (services == null)
                return AltOperationRegistry.Start(UniTask.FromResult(NotInstalledPayload()));

            UiAutomationServices ready = services;
            int frames = Mathf.Clamp(durationFrames, 2, 300);

            var request = new UiDeviceGestureRequest
            {
                Kind = UiDeviceGestureKind.Drag,
                From = new Vector2(fromX * Screen.width, (1f - fromY) * Screen.height),
                To = new Vector2(toX * Screen.width, (1f - toY) * Screen.height),
                DurationFrames = frames,
                Button = rightButton ? MouseButton.Right : MouseButton.Left,
            };

            return AltOperationRegistry.Start(
                ready.RunGestureAsync(request, (frames / ASSUMED_MIN_FPS) + GESTURE_TIMEOUT_GRACE_SEC, CancellationToken.None)
                     .ContinueWith(GesturePayload));
        }

        /// <summary>Virtual-mouse positional click at a normalized image point (full input-pipeline fidelity).</summary>
        public static int StartDeviceClick(float x, float y, bool rightButton)
        {
            if (services == null)
                return AltOperationRegistry.Start(UniTask.FromResult(NotInstalledPayload()));

            UiAutomationServices ready = services;

            var request = new UiDeviceGestureRequest
            {
                Kind = UiDeviceGestureKind.Click,
                To = new Vector2(x * Screen.width, (1f - y) * Screen.height),
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
                case "id" when int.TryParse(addressValue, out int instanceId):
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
