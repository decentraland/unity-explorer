using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.CharacterCamera.Components;
using DCL.SyntheticInput.Core;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using System;
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

        private static readonly QueryDescription CURSOR_QUERY = new QueryDescription().WithAll<CursorComponent>();

        private readonly World world;
        private readonly Entity playerEntity;

        // Resolved lazily: the cursor entity may not exist yet when the session is built.
        private Entity cursorEntity = Entity.Null;

        public UiDiscovery Discovery { get; }

        public UiInteractionSimulator Simulator { get; }

        public SdkUiResolver SdkResolver { get; }

        public AutomationVirtualDevices Devices { get; }

        public UiAutomationServices(World world, Entity playerEntity, EventSystem eventSystem, IScenesCache scenesCache)
        {
            this.world = world;
            this.playerEntity = playerEntity;

            Discovery = new UiDiscovery(eventSystem);
            SdkResolver = new SdkUiResolver(scenesCache);
            Simulator = new UiInteractionSimulator(eventSystem, SdkResolver);
            Devices = new AutomationVirtualDevices();
        }

        public void Dispose() =>
            Devices.Dispose();

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
        ///     Scene UI does not react to the virtual devices, so a drag inside it is synthesized against its elements.
        /// </summary>
        public async UniTask<SceneUiDragAttempt> DragSceneUiAsync(Vector2 fromImagePoint, Vector2 toImagePoint, int steps, CancellationToken ct)
        {
            if (!SdkResolver.TryGetScenePanel(out IPanel? panel, out string? noPanel) || panel == null)
                return SceneUiDragAttempt.NotApplicable(noPanel ?? "the current scene has no UI attached to a panel");

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
