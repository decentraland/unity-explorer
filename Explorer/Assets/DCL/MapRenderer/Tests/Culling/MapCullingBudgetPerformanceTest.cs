using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapCameraController;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using Unity.PerformanceTesting;
using UnityEngine;

namespace DCL.MapRenderer.Tests.Culling
{
    /// <summary>
    /// Guards fix #20 (part A): a single camera-dirty event (zoom / teleport with the map open) must
    /// NOT resolve every tracked marker in one frame. ResolveDirtyCameras now fans camera-dirtiness
    /// into the SAME budgeted <c>dirtyObjects</c> queue that ResolveDirtyObjects drains at
    /// MAX_DIRTY_OBJECTS_PER_FRAME/frame, so the "became visible" burst (pool.Get + DOTween spin-up)
    /// is spread across frames while still converging (no stale-hidden markers) and staying
    /// mutation-safe when StopTracking runs mid-drain.
    ///
    /// Falsification:
    ///  - Reverting to the old single-pass ResolveDirtyCameras makes the fan-out frame fire all N
    ///    became-visible callbacks at once => PeakBecameVisiblePerFrame == N &gt;&gt; BUDGET, failing
    ///    <see cref="TeleportBurst_IsBudgeted_AndEventuallyConsistent"/>.
    ///  - A drain that skips survivors under mutation (e.g. a persistent cursor over the trackedObjs
    ///    Dictionary, the reviewer-flagged defect of the first attempt) leaves a tracked marker
    ///    stale-hidden after StopTracking mid-drain, failing
    ///    <see cref="StopTrackingMidDrain_LeavesNoSurvivingMarkerStale"/>.
    /// </summary>
    [Category("Performance")]
    public class MapCullingBudgetPerformanceTest
    {
        // Mirrors MapCullingController.MAX_DIRTY_OBJECTS_PER_FRAME (private const).
        private const int BUDGET = 10;
        private const int MARKER_COUNT = 300;

        private static readonly Rect CAMERA_RECT = new (-5000, -5000, 10000, 10000);

        private sealed class ToggleVisibilityChecker : IMapCullingVisibilityChecker
        {
            public bool Visible;

            public bool IsVisible<T>(T obj, CameraState camera) where T: IMapPositionProvider =>
                Visible;
        }

        private sealed class RecordingListener : IMapCullingListener<IMapPositionProvider>
        {
            public bool Visible;
            public int BecameVisibleCalls;

            public void OnMapObjectBecameVisible(IMapPositionProvider obj)
            {
                Visible = true;
                BecameVisibleCalls++;
            }

            public void OnMapObjectCulled(IMapPositionProvider obj) =>
                Visible = false;
        }

        private ToggleVisibilityChecker checker = null!;
        private MapCullingController culling = null!;
        private IMapCameraControllerInternal camera = null!;
        private List<IMapPositionProvider> markers = null!;
        private List<RecordingListener> listeners = null!;

        [SetUp]
        public void SetUp()
        {
            checker = new ToggleVisibilityChecker();
            culling = new MapCullingController(checker);

            camera = Substitute.For<IMapCameraControllerInternal>();
            camera.GetCameraRect().Returns(CAMERA_RECT);
            culling.OnCameraAdded_Test(camera);

            markers = new List<IMapPositionProvider>(MARKER_COUNT);
            listeners = new List<RecordingListener>(MARKER_COUNT);

            for (int i = 0; i < MARKER_COUNT; i++)
            {
                var obj = Substitute.For<IMapPositionProvider>();
                obj.CurrentPosition.Returns(new Vector3(i, 0, 0));

                var listener = new RecordingListener();
                markers.Add(obj);
                listeners.Add(listener);
                ((IMapCullingController)culling).StartTracking(obj, listener);
            }

            // Settle every marker as culled (checker returns false): drains the initial per-object and
            // camera-add dirtiness so the queue is empty and nothing is visible before the teleport.
            checker.Visible = false;
            DrainToCompletion();

            Assert.AreEqual(0, culling.DirtyObjects.Count, "Setup should leave the dirty queue empty.");
            Assert.IsFalse(listeners.Exists(l => l.Visible), "No marker should be visible before the teleport.");
        }

        [TearDown]
        public void TearDown()
        {
            culling.Dispose();
        }

        [Test, Performance]
        public void TeleportBurst_IsBudgeted_AndEventuallyConsistent()
        {
            // Teleport / zoom: the whole marker set flips visible in one camera-dirty event.
            checker.Visible = true;
            ((IMapCullingController)culling).SetCameraDirty(camera);

            int totalBefore = TotalBecameVisible();
            var peakPerFrame = 0;

            // Frame 0: ResolveDirtyCameras must only ENQUEUE (0 became-visible), not resolve inline.
            int mark = TotalBecameVisible();
            culling.ResolveDirtyCameras_Test();
            peakPerFrame = Mathf.Max(peakPerFrame, TotalBecameVisible() - mark);

            // Subsequent frames: budgeted drain.
            var frames = 1;

            while (culling.DirtyObjects.Count > 0)
            {
                mark = TotalBecameVisible();
                culling.ResolveDirtyObjects_Test(BUDGET);
                peakPerFrame = Mathf.Max(peakPerFrame, TotalBecameVisible() - mark);
                frames++;
                Assert.Less(frames, MARKER_COUNT, "Drain did not converge within a sane frame budget.");
            }

            // Budget: no single frame may exceed the per-frame budget. The old single-pass code fired
            // all MARKER_COUNT callbacks on the fan-out frame, so this assertion falsifies a revert.
            Assert.LessOrEqual(peakPerFrame, BUDGET,
                $"Per-frame became-visible burst {peakPerFrame} exceeded the budget {BUDGET}; the camera-dirty resolution is not throttled.");

            // Eventual consistency: every marker became visible exactly once, none left stale-hidden.
            Assert.AreEqual(MARKER_COUNT, TotalBecameVisible() - totalBefore, "Not every marker became visible after the drain.");
            Assert.IsFalse(listeners.Exists(l => !l.Visible), "A marker was left stale-hidden after the drain.");
            Assert.IsTrue(listeners.TrueForAll(l => l.BecameVisibleCalls == 1), "A marker fired became-visible more than once.");

            int expectedMaxFrames = Mathf.CeilToInt((float)MARKER_COUNT / BUDGET) + 2;
            Assert.LessOrEqual(frames, expectedMaxFrames, $"Drain took {frames} frames, expected <= {expectedMaxFrames}.");

            // Timing: cost of resolving one teleport burst (fan-out + budgeted drain to completion).
            Measure.Method(() =>
                    {
                        checker.Visible = !checker.Visible;
                        ((IMapCullingController)culling).SetCameraDirty(camera);
                        culling.ResolveDirtyCameras_Test();

                        while (culling.DirtyObjects.Count > 0)
                            culling.ResolveDirtyObjects_Test(BUDGET);
                    })
                   .WarmupCount(3).MeasurementCount(20).GC().Run();
        }

        [Test]
        public void StopTrackingMidDrain_LeavesNoSurvivingMarkerStale()
        {
            checker.Visible = true;
            ((IMapCullingController)culling).SetCameraDirty(camera);

            // Frame 0: fan camera-dirtiness into the queue.
            culling.ResolveDirtyCameras_Test();

            // Drain two budgeted frames so a prefix of markers is already resolved (passed).
            culling.ResolveDirtyObjects_Test(BUDGET);
            culling.ResolveDirtyObjects_Test(BUDGET);

            // Mutate mid-drain: stop tracking an ALREADY-PASSED marker (index 0, resolved in frame 1)
            // and a NOT-YET-DRAINED tail marker still in the queue. A cursor-over-Dictionary drain
            // would skip a survivor after the removed entry; the LinkedList queue removes by node
            // reference and never skips a survivor.
            IMapPositionProvider removedPassed = markers[0];
            IMapPositionProvider removedQueued = markers[MARKER_COUNT - 1];
            culling.StopTracking(removedPassed);
            culling.StopTracking(removedQueued);

            // Finish the drain.
            var guard = 0;

            while (culling.DirtyObjects.Count > 0)
            {
                culling.ResolveDirtyObjects_Test(BUDGET);
                Assert.Less(++guard, MARKER_COUNT, "Drain did not converge after mutation.");
            }

            // Every SURVIVING marker must be visible; none stranded by the mid-drain removals.
            for (int i = 0; i < MARKER_COUNT; i++)
            {
                if (markers[i] == removedPassed || markers[i] == removedQueued)
                    continue;

                Assert.IsTrue(listeners[i].Visible, $"Surviving marker {i} was left stale-hidden after StopTracking mid-drain.");
            }
        }

        // Fan camera-dirtiness into the budgeted queue and drain it fully (unbounded per call). Used by
        // SetUp to settle the initial StartTracking / camera-add dirtiness before the measured teleport.
        private void DrainToCompletion()
        {
            culling.ResolveDirtyCameras_Test();

            while (culling.DirtyObjects.Count > 0)
                culling.ResolveDirtyObjects_Test(int.MaxValue);
        }

        private int TotalBecameVisible()
        {
            var sum = 0;

            foreach (RecordingListener l in listeners)
                sum += l.BecameVisibleCalls;

            return sum;
        }
    }
}
