using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.SyntheticInput.UiSimulation;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.SyntheticInput.Tests
{
    public class UiAutomationServicesShould
    {
        private World world = null!;
        private GameObject eventSystemGo = null!;
        private UiAutomationServices services = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            Entity playerEntity = world.Create();

            eventSystemGo = new GameObject("test-event-system");
            var eventSystem = eventSystemGo.AddComponent<EventSystem>();

            var scenesCache = Substitute.For<IScenesCache>();
            scenesCache.CurrentScene.Returns(new ReactiveProperty<ISceneFacade?>(null));

            services = new UiAutomationServices(world, playerEntity, eventSystem, scenesCache);
        }

        [TearDown]
        public void TearDown()
        {
            services.Dispose();
            Object.DestroyImmediate(eventSystemGo);
            World.Destroy(world);
        }

        [Test]
        public void ExplainWhyASceneUiDragDoesNotApply()
        {
            UniTask<SceneUiDragAttempt> drag = services.DragSceneUiAsync(new Vector2(10f, 10f), new Vector2(20f, 20f), steps: 4, CancellationToken.None);

            // A caller falling back to the virtual devices would drag the 3D world, so declining is not enough:
            // without the reason a failed scene-UI drag is indistinguishable from a delivered one.
            Assert.That(drag.Status, Is.EqualTo(UniTaskStatus.Succeeded));

            SceneUiDragAttempt attempt = drag.GetAwaiter().GetResult();
            Assert.That(attempt.Result, Is.Null);
            Assert.That(attempt.SkipReason, Is.EqualTo("no running current scene"));
        }

        [Test]
        public void ReportNoUiCoverWhenNothingIsOnScreen()
        {
            Assert.That(services.TryFindUiCoverAt(new Vector2(5f, 5f), out string cover), Is.False);
            Assert.That(cover, Is.Empty);
        }

        [Test]
        public void ReportBothDragEndsOverTheWorldWhenNoUiIsOnScreen()
        {
            services.DescribeDragCover(new Vector2(5f, 5f), new Vector2(50f, 50f), out string? coverAtStart, out string? coverAtEnd);

            // Null is the world: the empty cover string TryFindUiCoverAt leaves behind would read as a named cover.
            Assert.That(coverAtStart, Is.Null);
            Assert.That(coverAtEnd, Is.Null);
        }

        [Test]
        public void NoteThatNoUiReceivedADragOverTheWorld()
        {
            UiDeviceDragOutcome outcome = UiDeviceDragOutcome.From(new UiGestureResult { Ok = true }, null, null);

            // The gesture verifies no target, so a bare success here would read as a delivered drag.
            Assert.That(outcome.Ok, Is.True);
            Assert.That(outcome.DeliveryNote, Does.Contain("no UI element received this drag"));
            Assert.That(outcome.DeliveryNote, Does.Contain("sweep_pointer"));
        }

        [Test]
        public void AddNoNoteWhenTheDragStartedOnUi()
        {
            UiDeviceDragOutcome outcome = UiDeviceDragOutcome.From(new UiGestureResult { Ok = true }, "MainUI/Sidebar/ExploreButton", null);

            // A drag that began on an element is a plain uGUI drag: the element keeps the pointer to the release.
            Assert.That(outcome.CoverAtStart, Is.EqualTo("MainUI/Sidebar/ExploreButton"));
            Assert.That(outcome.CoverAtEnd, Is.Null);
            Assert.That(outcome.DeliveryNote, Is.Null);
        }

        [Test]
        public void AddNoNoteToAFailedDrag()
        {
            UiDeviceDragOutcome outcome = UiDeviceDragOutcome.From(
                new UiGestureResult { Ok = false, FailureReason = "the drag panned the camera instead of dragging" }, null, null);

            // A failure carries its own reason; a note beside it would compete with it.
            Assert.That(outcome.Ok, Is.False);
            Assert.That(outcome.FailureReason, Does.Contain("panned the camera"));
            Assert.That(outcome.DeliveryNote, Is.Null);
        }
    }
}
