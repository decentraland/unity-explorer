using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.CharacterCamera.Components;
using DCL.SyntheticInput.Core;
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
    ///     Reports what UI covers a screen point, if anything. A one-method seam so the pointer system can consult
    ///     the UI sublayer without taking its whole session object (which needs a World, an Entity, an EventSystem
    ///     and a scenes cache to build) — the live implementation is
    ///     <see cref="UiAutomationServices.TryFindUiCoverAt" />, tests pass a stub.
    /// </summary>
    public delegate bool UiCoverProbe(Vector2 screenPoint, out string cover);

    /// <summary>
    ///     The UI half of the synthetic input layer, constructed once per automation session: element discovery
    ///     and resolution, the semantic simulator, and the virtual devices with their gesture pipeline. Both
    ///     driver front-ends (MCP tools, AltTester probes) act through this one instance.
    /// </summary>
    public class UiAutomationServices : IDisposable
    {
        /// <summary>Frames are the driver's unit, seconds are the timeout's: a pessimistic frame rate converts one to the other.</summary>
        private const float ASSUMED_MIN_FPS = 15f;
        private const float GESTURE_TIMEOUT_GRACE_SEC = 5f;

        private static readonly QueryDescription CURSOR_QUERY = new QueryDescription().WithAll<CursorComponent>();

        private readonly World world;
        private readonly Entity playerEntity;

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
        ///     What UI, if anything, covers a screen point (Unity screen coordinates) — client interface first,
        ///     then the current scene's own UI. A screen-addressed world click must fail against this instead of
        ///     aiming through it: the ray would reach an entity the pixel's real owner would have intercepted. The
        ///     cover names what a driver can act on: the scene element's CRDT id where the scene owns the point,
        ///     the element path where the client interface does.
        /// </summary>
        public bool TryFindUiCoverAt(Vector2 screenPoint, out string cover)
        {
            if (Discovery.TryFindCoverAt(screenPoint, out string? uguiPath, out IPanel? hostedPanel))
            {
                // The uGUI raycast reports a covering UI Toolkit panel as its host GameObject, so for scene UI the
                // path names Unity plumbing ("EventSystem/DCLScenePanelSettings"). Re-derive the pick inside that
                // panel to name the element instead: its CRDT id is the address ui_click takes.
                if (hostedPanel != null && SdkResolver.TryDescribeCoverIn(hostedPanel, screenPoint, out string? hostedCover))
                {
                    cover = hostedCover!;
                    return true;
                }

                cover = uguiPath!;
                return true;
            }

            // Reached when the scene's panel raycasts nothing at the point (no PanelRaycaster registered) yet the
            // panel itself picks an element there; the panel's own hit test is what a real click would obey.
            if (SdkResolver.TryFindCoverAt(screenPoint, out string? sdkCover))
            {
                cover = sdkCover!;
                return true;
            }

            cover = string.Empty;
            return false;
        }

        /// <summary>
        ///     The listing both driver front-ends hand back: the requested stacks' interactable elements, plus the
        ///     screen size their rects are expressed in. Stating the screen is what makes a rect usable — a
        ///     screenshot may be downscaled from it, so rects normalize against this and nothing else.
        /// </summary>
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

        /// <summary>
        ///     Runs a virtual-device gesture through UiVirtualDeviceGestureSystem; a gesture the simulation never
        ///     completed is abandoned and reported as a failure. Main thread only.
        /// </summary>
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
        ///     Replays a drag through the virtual devices and reports what its pointer was over. A device gesture
        ///     verifies no target — completing only means the states were replayed — so the covers at both ends are
        ///     what tells a caller whether any UI could have received it: over the world at both ends, nothing did.
        ///     Owns the gesture's timeout, derived from the requested frame count. Main thread only.
        /// </summary>
        public async UniTask<UiDeviceDragOutcome> DragWithDevicesAsync(Vector2 fromScreenPoint, Vector2 toScreenPoint,
            int durationFrames, MouseButton button, CancellationToken ct)
        {
            // Read before the gesture is installed, because the drag moves the pointer itself; null is the world.
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
        ///     Drags inside the scene's own UI, if the scene UI is what sits under <paramref name="fromImagePoint" />:
        ///     UI Toolkit panels consume events sent to their elements rather than virtual-device pointer state, so a
        ///     positional gesture there has to be synthesized against the elements. When the scene UI does not own
        ///     the point the attempt reports <em>why</em> instead of just declining: a caller falling back to the
        ///     virtual devices would otherwise deliver a world drag that reads exactly like a delivered UI one.
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

        /// <summary>The cursor state name, for driver-facing diagnostics (clicks on invisible-under-lock UI are allowed but flagged).</summary>
        public string CursorStateName()
        {
            var stateName = "unknown";

            world.Query(in CURSOR_QUERY, (ref CursorComponent cursor) => stateName = cursor.CursorState.ToString());

            return stateName;
        }
    }

    /// <summary>
    ///     The outcome of trying to drag inside the scene's own UI: either the semantic path ran and produced a
    ///     <see cref="UiActionResult" />, or it did not apply and says why. A driver that falls back to the virtual
    ///     devices owes its caller that reason — a fallback drag lands in the 3D world, and without the reason it is
    ///     indistinguishable from a UI drag that was really delivered.
    /// </summary>
    public struct SceneUiDragAttempt
    {
        /// <summary>What the semantic drag achieved, or null when that path did not apply.</summary>
        public UiActionResult? Result;

        /// <summary>Why the semantic path did not apply, or null when it ran.</summary>
        public string? SkipReason;

        public static SceneUiDragAttempt Delivered(UiActionResult result) =>
            new () { Result = result };

        public static SceneUiDragAttempt NotApplicable(string reason) =>
            new () { SkipReason = reason };
    }

    /// <summary>
    ///     What a virtual-device drag achieved. The gesture itself verifies no target — completing means the mouse
    ///     states were replayed — so the covers read at both ends before it ran are the only evidence of what could
    ///     have received it, and a drag whose pointer was over the world at both ends reached no UI at all. Saying
    ///     so is the point: an unqualified "ok" there reads as a delivered drag, which is the one thing it is not.
    /// </summary>
    public struct UiDeviceDragOutcome
    {
        public bool Ok;

        public string? FailureReason;

        /// <summary>What covered the drag's start pixel, or null when the world did.</summary>
        public string? CoverAtStart;

        /// <summary>What covered the drag's end pixel, or null when the world did.</summary>
        public string? CoverAtEnd;

        /// <summary>Set only for a delivered drag no UI could have received; null whenever there is nothing to add.</summary>
        public string? DeliveryNote;

        public static UiDeviceDragOutcome From(in UiGestureResult gesture, string? coverAtStart, string? coverAtEnd) =>
            new ()
            {
                Ok = gesture.Ok,
                FailureReason = gesture.FailureReason,
                CoverAtStart = coverAtStart,
                CoverAtEnd = coverAtEnd,

                // A failure already carries its own reason (a captured cursor, a timeout, a preemption); the note
                // is only for the case a bare success would misreport.
                DeliveryNote = gesture.Ok && coverAtStart == null && coverAtEnd == null
                    ? "no UI element received this drag: the pointer was over the world at both ends (nothing in the "
                      + "client interface or the scene's UI covers either pixel). A held pointer swept across the world is "
                      + "a press, a camera turn and a release — sweep_pointer."
                    : null,
            };
    }
}
