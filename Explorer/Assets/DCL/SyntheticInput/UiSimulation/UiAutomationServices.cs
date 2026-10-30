using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.CharacterCamera.Components;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SyntheticInput.Core;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using MouseButton = UnityEngine.InputSystem.LowLevel.MouseButton;

namespace DCL.SyntheticInput.UiSimulation
{
    /// <summary>
    ///     A one-method seam, so the pointer system does not depend on the whole <see cref="UiAutomationServices" />.
    /// </summary>
    public delegate bool UiCoverProbe(Vector2 screenPoint, out string cover);

    /// <summary>The UI half of the synthetic input layer. Built once per automation session.</summary>
    public class UiAutomationServices : IDisposable
    {
        // Pessimistic frame rate for converting a frame count into a timeout.
        private const float ASSUMED_MIN_FPS = 15f;
        private const float GESTURE_TIMEOUT_GRACE_SEC = 5f;

        // Frames the virtual mouse rests on the element before a device hover reports; the input module needs one
        // frame to process the move.
        private const int DEVICE_HOVER_FRAMES = 2;

        private static readonly QueryDescription CURSOR_QUERY = new QueryDescription().WithAll<CursorComponent>();

        // A device hover ends by moving the virtual mouse here, where nothing raycasts.
        private static readonly Vector2 OFF_SCREEN_POINT = new (-1f, -1f);

        private readonly World world;
        private readonly Entity playerEntity;

        // The one parked hover; parkedChain holds the uGUI objects a semantic hover entered.
        private readonly List<GameObject> parkedChain = new ();
        private ParkedHoverKind parkedKind;
        private string parkedAddress = string.Empty;
        private UITransformComponent? parkedSdkSlot;

        // Resolved lazily: the cursor entity may not exist yet when the session is built.
        private Entity cursorEntity = Entity.Null;

        public UiDiscovery Discovery { get; }

        public UiInteractionSimulator Simulator { get; }

        public SdkUiResolver SdkResolver { get; }

        public AutomationVirtualDevices Devices { get; }

        /// <summary>The address the parked hover rests on, null when none is parked.</summary>
        public string? HoveredAddress => parkedKind == ParkedHoverKind.None ? null : parkedAddress;

        public UiAutomationServices(World world, Entity playerEntity, EventSystem eventSystem, IScenesCache scenesCache)
        {
            this.world = world;
            this.playerEntity = playerEntity;

            Discovery = new UiDiscovery(eventSystem);
            SdkResolver = new SdkUiResolver(scenesCache);
            Simulator = new UiInteractionSimulator(eventSystem, SdkResolver);
            Devices = new AutomationVirtualDevices();
        }

        public void Dispose()
        {
            EndHover();
            Devices.Dispose();
        }

        /// <summary>
        ///     Parks a hover on a scene UI element: the enter is delivered and drained, and the hover stays until
        ///     <see cref="EndHover" />. Any hover parked before is ended first. Main thread only.
        /// </summary>
        public async UniTask<UiActionResult> HoverSdkAsync(UiElementAddress address, SdkUiElement element, bool force, CancellationToken ct)
        {
            string? endedHover = EndHover();
            (UiActionResult result, bool enterSent) = await Simulator.HoverSdkEnterAsync(element, force, ct);
            result.EndedHover = endedHover;

            // An enter the scene has not consumed yet still reaches it later, so it is parked to be left.
            if (enterSent)
                Park(ParkedHoverKind.Sdk, address, element.Transform);

            return result;
        }

        /// <summary>
        ///     Parks a hover on a client UI element, by synthesizing its enter events or, with
        ///     <paramref name="device" />, by resting the virtual mouse on its center. Any hover parked before is ended
        ///     first. Main thread only.
        /// </summary>
        public async UniTask<UiHoverOutcome> HoverUguiAsync(UiElementAddress address, GameObject target, bool force, bool device,
            float timeoutSec, CancellationToken ct)
        {
            string? endedHover = EndHover();

            if (!device)
            {
                UiActionResult semantic = Simulator.HoverUguiEnter(target, force, parkedChain);
                semantic.EndedHover = endedHover;

                if (semantic.Ok)
                    Park(ParkedHoverKind.UguiSemantic, address, null);

                return new UiHoverOutcome { Result = semantic };
            }

            var rectTransform = (RectTransform)target.transform;
            Vector2 center = UiScreenGeometry.ScreenCenterOf(rectTransform);

            // Read before the gesture, while the pixel still shows what the pointer is about to rest on.
            string? cover = TryFindUiCoverAt(center, out string coverAtCenter) ? coverAtCenter : null;

            UiGestureResult gesture = await RunGestureAsync(new UiDeviceGestureRequest
            {
                Kind = UiDeviceGestureKind.Hover,
                From = center,
                To = center,
                DurationFrames = DEVICE_HOVER_FRAMES,
            }, timeoutSec, ct);

            Rect imageRect = UiScreenGeometry.ImageRectOf(rectTransform);

            UiActionResult result = gesture.Ok
                ? UiActionResult.Success(imageRect)
                : UiActionResult.Failure(gesture.FailureReason ?? "the device hover failed", null, imageRect);

            result.EndedHover = endedHover;

            if (!gesture.Ok)
            {
                // The gesture may have moved the pointer before it failed, so it is moved where nothing stays hovered.
                Devices.QueueMouseState(OFF_SCREEN_POINT);
                return new UiHoverOutcome { Result = result, PointerOver = cover, ReportsPointerOver = true };
            }

            Park(ParkedHoverKind.UguiDevice, address, null);

            if (cover == null)
                result.Info = "no UI element is under the element's center: the virtual mouse rests over the world, so no UI received the hover";

            return new UiHoverOutcome { Result = result, PointerOver = cover, ReportsPointerOver = true };
        }

        /// <summary>
        ///     Delivers the leave of the parked hover, if any, and returns the address it was parked on. Synchronous,
        ///     so every UI gesture can end the hover before it starts. Main thread only.
        /// </summary>
        public string? EndHover()
        {
            if (parkedKind == ParkedHoverKind.None)
                return null;

            string ended = parkedAddress;

            switch (parkedKind)
            {
                case ParkedHoverKind.Sdk:
                    if (parkedSdkSlot is { Transform: { panel: not null } })
                        Simulator.SendSdkLeave(parkedSdkSlot.Transform, parkedSdkSlot);
                    else
                        ended += " (the element is gone; no leave was sent)";

                    break;
                case ParkedHoverKind.UguiSemantic:
                    Simulator.HoverUguiLeave(parkedChain);
                    break;
                case ParkedHoverKind.UguiDevice:
                    Devices.QueueMouseState(OFF_SCREEN_POINT);
                    break;
            }

            parkedKind = ParkedHoverKind.None;
            parkedAddress = string.Empty;
            parkedSdkSlot = null;
            parkedChain.Clear();

            return ended;
        }

        private void Park(ParkedHoverKind kind, UiElementAddress address, UITransformComponent? sdkSlot)
        {
            parkedKind = kind;
            parkedAddress = address.ToString();
            parkedSdkSlot = sdkSlot;
        }

        /// <summary>
        ///     Which UI covers a screen point (Unity screen coordinates): the client interface is checked first, then
        ///     the current scene's UI. The cover is the element path for client UI and the CRDT id for scene UI.
        /// </summary>
        public bool TryFindUiCoverAt(Vector2 screenPoint, out string cover)
        {
            if (Discovery.TryFindCoverAt(screenPoint, out string? uguiPath, out IPanel? hostedPanel))
            {
                if (hostedPanel != null && SdkResolver.TryDescribeCoverIn(hostedPanel, screenPoint, out string? hostedCover))
                {
                    cover = hostedCover;
                    return true;
                }

                cover = uguiPath;
                return true;
            }

            // Reached when the scene's panel raycasts nothing at the point (no PanelRaycaster registered) yet the
            // panel itself picks an element there; the panel's own hit test is what a real click would obey.
            if (SdkResolver.TryFindCoverAt(screenPoint, out string? sdkCover))
            {
                cover = sdkCover;
                return true;
            }

            cover = string.Empty;
            return false;
        }

        /// <summary>The requested stacks' interactable elements, with the screen size their rects are in.</summary>
        public JObject ListInteractableJson(bool includeUgui, bool includeSdk, bool checkOcclusion)
        {
            var elements = new JArray();

            if (includeUgui)
                foreach (JToken entry in Discovery.ListInteractable(checkOcclusion))
                    elements.Add(entry);

            if (includeSdk)
                foreach (JToken entry in SdkResolver.ListInteractable())
                    elements.Add(entry);

            return new JObject
            {
                ["count"] = elements.Count,
                ["elements"] = elements,
                ["screen"] = UiDiscovery.ScreenJson(),
            };
        }

        /// <summary>Runs a virtual-device gesture. Main thread only.</summary>
        public async UniTask<UiGestureResult> RunGestureAsync(UiDeviceGestureRequest request, float timeoutSec, CancellationToken ct)
        {
            UniTask<UiGestureResult> gesture = EcsRequest.SendAsync(world, playerEntity, request,
                new UiGestureResult { Ok = false, FailureReason = "preempted by a newer gesture" });

            try
            {
                return await gesture.AttachExternalCancellation(ct)
                                    .Timeout(TimeSpan.FromSeconds(timeoutSec));
            }
            catch (TimeoutException)
            {
                await EcsRequest.AbandonAsync<UiDeviceGestureRequest>(world, playerEntity);
                return new UiGestureResult { Ok = false, FailureReason = $"the gesture did not complete within {timeoutSec}s (is the simulation paused?)" };
            }
        }

        /// <summary>
        ///     Clicks a scene-UI element through the virtual mouse, one input state per scene drain: the element's
        ///     single pointer-event slot would otherwise be overwritten by the next edge before the throttled scene
        ///     system reads it. The result says which events the scene consumed. Main thread only.
        /// </summary>
        public async UniTask<UiActionResult> ClickSdkWithDevicesAsync(SdkUiElement element, MouseButton button, bool force, float timeoutSec, CancellationToken ct)
        {
            VisualElement target = element.Transform.Transform;
            UITransformComponent slot = element.Transform;

            if (!Simulator.TryCheckSdkPointer(target, force, "click", out UiActionResult blocked, out Rect imageRect))
                return blocked;

            if (!await Simulator.SettlePendingSdkEventsAsync(slot, ct))
                return UiActionResult.Failure("the scene did not consume an earlier pointer event on the element (is the scene paused?); the click was not delivered", null, imageRect);

            Vector2 center = UiScreenGeometry.ImageToScreenPoint(imageRect.center);

            var delivery = new SdkClickDelivery
            {
                DeclaresDown = SdkClickVerdict.Declares(element.PointerEvents, PointerEventType.PetDown),
                DeclaresUp = SdkClickVerdict.Declares(element.PointerEvents, PointerEventType.PetUp),
            };

            bool declaresEnter = SdkClickVerdict.Declares(element.PointerEvents, PointerEventType.PetHoverEnter);

            // Off the element first, so the pointer's arrival produces the enter even when the mouse already rests there.
            Devices.QueueMouseState(OFF_SCREEN_POINT);
            await UniTask.Yield(PlayerLoopTiming.Update, ct);

            var pressed = false;

            try
            {
                (UiGestureResult moved, SdkDrainObservation enter) = await RunPacedEdgeAsync(UiDeviceGestureKind.MoveTo, center, button, slot, PointerEventType.PetHoverEnter, declaresEnter, timeoutSec, ct);

                if (!moved.Ok)
                    return UiActionResult.Failure(moved.FailureReason ?? "the device click failed", null, imageRect);

                delivery.Enter = SdkClickVerdict.Judge(PointerEventType.PetHoverEnter, declaresEnter, in enter);

                (UiGestureResult downEdge, SdkDrainObservation down) = await RunPacedEdgeAsync(UiDeviceGestureKind.ButtonDown, center, button, slot, PointerEventType.PetDown, delivery.DeclaresDown, timeoutSec, ct);
                pressed = downEdge.Ok;

                if (!downEdge.Ok)
                    return UiActionResult.Failure(downEdge.FailureReason ?? "the device click failed", null, imageRect);

                delivery.Down = SdkClickVerdict.Judge(PointerEventType.PetDown, delivery.DeclaresDown, in down);

                (UiGestureResult upEdge, SdkDrainObservation up) = await RunPacedEdgeAsync(UiDeviceGestureKind.ButtonUp, center, button, slot, PointerEventType.PetUp, delivery.DeclaresUp, timeoutSec, ct);
                pressed = pressed && !upEdge.Ok;

                if (!upEdge.Ok)
                    return UiActionResult.Failure(upEdge.FailureReason ?? "the device click failed", null, imageRect);

                delivery.Up = SdkClickVerdict.Judge(PointerEventType.PetUp, delivery.DeclaresUp, in up);

                return SdkClickVerdict.Build(in delivery, imageRect, in down, in up);
            }
            finally
            {
                // A held button must never outlive the click; the move off the element is what produces its leave.
                if (pressed)
                    Devices.QueueMouseState(center);

                Devices.QueueMouseState(OFF_SCREEN_POINT);
                Simulator.NoteUnsettledLeave(slot);
            }
        }

        /// <summary>Clicks a client UI element positionally through the virtual mouse. Main thread only.</summary>
        public async UniTask<UiActionResult> ClickWithDevicesAsync(Vector2 screenCenter, Rect imageRect, MouseButton button, float timeoutSec, CancellationToken ct)
        {
            UiGestureResult gesture = await RunGestureAsync(new UiDeviceGestureRequest
            {
                Kind = UiDeviceGestureKind.Click,
                To = screenCenter,
                Button = button,
            }, timeoutSec, ct);

            return gesture.Ok
                ? UiActionResult.Success(imageRect)
                : UiActionResult.Failure(gesture.FailureReason ?? "the device click failed", null, imageRect);
        }

        /// <summary>One queued pointer state, then the scene's drain of the event it should have produced.</summary>
        private async UniTask<(UiGestureResult Gesture, SdkDrainObservation Observed)> RunPacedEdgeAsync(UiDeviceGestureKind kind, Vector2 point, MouseButton button,
            UITransformComponent slot, PointerEventType sent, bool declared, float timeoutSec, CancellationToken ct)
        {
            int drainsBefore = slot.Drains.DrainCount;

            UiGestureResult gesture = await RunGestureAsync(new UiDeviceGestureRequest
            {
                Kind = kind,
                From = point,
                To = point,
                DurationFrames = 1,
                Button = button,
            }, timeoutSec, ct);

            if (!gesture.Ok)
                return (gesture, SdkDrainObservation.NotDrained);

            return (gesture, await Simulator.ObserveOrSettleAsync(slot, sent, declared, drainsBefore, ct));
        }

        /// <summary>Drags through the virtual devices. Main thread only.</summary>
        public async UniTask<UiDeviceDragOutcome> DragWithDevicesAsync(Vector2 fromScreenPoint, Vector2 toScreenPoint,
            int durationFrames, MouseButton button, CancellationToken ct)
        {
            // Read before the gesture runs, because the drag moves the pointer.
            string? coverAtStart = TryFindUiCoverAt(fromScreenPoint, out string startCover) ? startCover : null;
            string? coverAtEnd = TryFindUiCoverAt(toScreenPoint, out string endCover) ? endCover : null;

            UiGestureResult gesture = await RunGestureAsync(new UiDeviceGestureRequest
            {
                Kind = UiDeviceGestureKind.Drag,
                From = fromScreenPoint,
                To = toScreenPoint,
                DurationFrames = durationFrames,
                Button = button,
            }, (durationFrames / ASSUMED_MIN_FPS) + GESTURE_TIMEOUT_GRACE_SEC, ct);

            return UiDeviceDragOutcome.From(in gesture, coverAtStart, coverAtEnd);
        }

        /// <summary>
        ///     A drag inside the scene UI is synthesized against its elements: the virtual mouse does reach the scene's
        ///     panel, but a drag writes more events per frame than the element's single pointer-event slot can hold.
        /// </summary>
        public async UniTask<SceneUiDragAttempt> DragSceneUiAsync(Vector2 fromImagePoint, Vector2 toImagePoint, int steps, CancellationToken ct)
        {
            if (!SdkResolver.TryGetScenePanel(out IPanel? panel, out string? noPanel))
                return SceneUiDragAttempt.NotApplicable(noPanel);

            if (panel.Pick(UiScreenGeometry.ImageToPanelPoint(panel, fromImagePoint)) == null)
                return SceneUiDragAttempt.NotApplicable(
                    "the scene's UI does not cover the drag start point (nothing pickable there — the UI may still be "
                    + "attaching or laying out, and only elements declared with pointerFilter PFM_BLOCK are pickable); "
                    + "ui_list stack:sdk shows what is attached");

            return SceneUiDragAttempt.Delivered(await Simulator.DragSdkAsync(panel, fromImagePoint, toImagePoint, steps, ct));
        }

        public string CursorStateName()
        {
            if (cursorEntity == Entity.Null)
                cursorEntity = world.GetSingleInstanceEntityOrNull(in CURSOR_QUERY);

            return cursorEntity != Entity.Null && world.TryGet(cursorEntity, out CursorComponent cursor)
                ? CursorStateNameOf(cursor.CursorState)
                : "unknown";
        }

        private static string CursorStateNameOf(CursorState state) =>
            state switch
            {
                CursorState.Free => nameof(CursorState.Free),
                CursorState.Locked => nameof(CursorState.Locked),
                CursorState.Panning => nameof(CursorState.Panning),
                CursorState.LockedWithUi => nameof(CursorState.LockedWithUi),
                _ => "unknown",
            };
    }

    internal enum ParkedHoverKind : byte
    {
        None,
        Sdk,
        UguiSemantic,
        UguiDevice,
    }

    /// <summary>
    ///     A uGUI hover's result. The device path also reports what covered the element's center before the pointer
    ///     moved there, null where the world was under the pixel.
    /// </summary>
    public struct UiHoverOutcome
    {
        public UiActionResult Result;

        public bool ReportsPointerOver;

        public string? PointerOver;
    }

    /// <summary>Exactly one of <see cref="Result" /> and <see cref="SkipReason" /> is set.</summary>
    public struct SceneUiDragAttempt
    {
        public UiActionResult? Result;

        public string? SkipReason;

        public static SceneUiDragAttempt Delivered(UiActionResult result) =>
            new () { Result = result };

        public static SceneUiDragAttempt NotApplicable(string reason) =>
            new () { SkipReason = reason };
    }

    /// <summary>
    ///     Ok means the mouse states were replayed, not that any UI received them. The covers were read before the
    ///     drag ran, and are null where the world was under the pixel.
    /// </summary>
    public struct UiDeviceDragOutcome
    {
        public bool Ok;

        public string? FailureReason;

        public string? CoverAtStart;

        public string? CoverAtEnd;

        public string? DeliveryNote;

        public static UiDeviceDragOutcome From(in UiGestureResult gesture, string? coverAtStart, string? coverAtEnd) =>
            new ()
            {
                Ok = gesture.Ok,
                FailureReason = gesture.FailureReason,
                CoverAtStart = coverAtStart,
                CoverAtEnd = coverAtEnd,

                DeliveryNote = gesture.Ok && coverAtStart == null && coverAtEnd == null
                    ? "no UI element received this drag: the pointer was over the world at both ends (nothing in the "
                      + "client interface or the scene's UI covers either pixel). A held pointer swept across the world is "
                      + "a press, a camera turn and a release — sweep_pointer."
                    : null,
            };
    }
}
