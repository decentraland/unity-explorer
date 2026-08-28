using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.CharacterCamera.Components;
using DCL.SyntheticInput.Core;
using ECS.SceneLifeCycle;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace DCL.SyntheticInput.UiSimulation
{
    /// <summary>
    ///     The UI half of the synthetic input layer, constructed once per automation session: element discovery
    ///     and resolution, the semantic simulator, and the virtual devices with their gesture pipeline. Both
    ///     driver front-ends (MCP tools, AltTester probes) act through this one instance.
    /// </summary>
    public class UiAutomationServices : IDisposable
    {
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
            Simulator = new UiInteractionSimulator(eventSystem);
            SdkResolver = new SdkUiResolver(scenesCache);
            Devices = new AutomationVirtualDevices();
        }

        public void Dispose() =>
            Devices.Dispose();

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
        ///     Drags inside the scene's own UI, if the scene UI is what sits under <paramref name="fromImagePoint" />:
        ///     UI Toolkit panels consume events sent to their elements rather than virtual-device pointer state, so a
        ///     positional gesture there has to be synthesized against the elements. Returns null when the scene UI
        ///     does not own the point, leaving the gesture to the virtual devices.
        /// </summary>
        public async UniTask<UiActionResult?> TryDragSceneUiAsync(Vector2 fromImagePoint, Vector2 toImagePoint, int steps, CancellationToken ct)
        {
            if (!SdkResolver.TryGetScenePanel(out IPanel? panel, out _) || panel == null)
                return null;

            if (panel.Pick(UiScreenGeometry.ImageToPanelPoint(panel, fromImagePoint)) == null)
                return null;

            return await Simulator.DragSdkAsync(panel, fromImagePoint, toImagePoint, steps, ct);
        }

        /// <summary>The cursor state name, for driver-facing diagnostics (clicks on invisible-under-lock UI are allowed but flagged).</summary>
        public string CursorStateName()
        {
            var stateName = "unknown";

            world.Query(in CURSOR_QUERY, (ref CursorComponent cursor) => stateName = cursor.CursorState.ToString());

            return stateName;
        }
    }
}
