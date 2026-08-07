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
    /// A single camera-dirty event (zoom / teleport with the map open) must NOT resolve every tracked
    /// marker in one frame. ResolveDirtyCameras fans camera-dirtiness into the same budgeted
    /// <c>dirtyObjects</c> queue that ResolveDirtyObjects drains at MAX_DIRTY_OBJECTS_PER_FRAME/frame,
    /// so the "became visible" burst (pool.Get + DOTween spin-up) spreads across frames while still
    /// converging (no stale-hidden markers) and staying mutation-safe when StopTracking runs
    /// mid-drain.
    ///
    /// <para>
    /// <see cref="TeleportBurst_IsBudgeted_AndEventuallyConsistent"/> asserts the per-frame
    /// became-visible burst never exceeds the budget and that every marker eventually converges to
    /// visible. <see cref="StopTrackingMidDrain_LeavesNoSurvivingMarkerStale"/> asserts that removing a
    /// marker from tracking mid-drain doesn't leave a surviving marker stale-hidden — a cursor over
    /// the trackedObjs dictionary that skips survivors under mutation would fail this.
    /// </para>
    /// </summary>
    [Category("Performance")]
    public class MapCullingBudgetPerformanceTest
    {
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
            checker.Visible = true;
            ((IMapCullingController)culling).SetCameraDirty(camera);

            int totalBefore = TotalBecameVisible();
            var peakPerFrame = 0;

            int mark = TotalBecameVisible();
            culling.ResolveDirtyCameras_Test();
            peakPerFrame = Mathf.Max(peakPerFrame, TotalBecameVisible() - mark);

            var frames = 1;

            while (culling.DirtyObjects.Count > 0)
            {
                mark = TotalBecameVisible();
                culling.ResolveDirtyObjects_Test(BUDGET);
                peakPerFrame = Mathf.Max(peakPerFrame, TotalBecameVisible() - mark);
                frames++;
                Assert.Less(frames, MARKER_COUNT, "Drain did not converge within a sane frame budget.");
            }

            Assert.LessOrEqual(peakPerFrame, BUDGET,
                $"Per-frame became-visible burst {peakPerFrame} exceeded the budget {BUDGET}; the camera-dirty resolution is not throttled.");

            Assert.AreEqual(MARKER_COUNT, TotalBecameVisible() - totalBefore, "Not every marker became visible after the drain.");
            Assert.IsFalse(listeners.Exists(l => !l.Visible), "A marker was left stale-hidden after the drain.");
            Assert.IsTrue(listeners.TrueForAll(l => l.BecameVisibleCalls == 1), "A marker fired became-visible more than once.");

            int expectedMaxFrames = Mathf.CeilToInt((float)MARKER_COUNT / BUDGET) + 2;
            Assert.LessOrEqual(frames, expectedMaxFrames, $"Drain took {frames} frames, expected <= {expectedMaxFrames}.");

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

            culling.ResolveDirtyCameras_Test();

            culling.ResolveDirtyObjects_Test(BUDGET);
            culling.ResolveDirtyObjects_Test(BUDGET);

            IMapPositionProvider removedPassed = markers[0];
            IMapPositionProvider removedQueued = markers[MARKER_COUNT - 1];
            culling.StopTracking(removedPassed);
            culling.StopTracking(removedQueued);

            var guard = 0;

            while (culling.DirtyObjects.Count > 0)
            {
                culling.ResolveDirtyObjects_Test(BUDGET);
                Assert.Less(++guard, MARKER_COUNT, "Drain did not converge after mutation.");
            }

            for (int i = 0; i < MARKER_COUNT; i++)
            {
                if (markers[i] == removedPassed || markers[i] == removedQueued)
                    continue;

                Assert.IsTrue(listeners[i].Visible, $"Surviving marker {i} was left stale-hidden after StopTracking mid-drain.");
            }
        }

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
