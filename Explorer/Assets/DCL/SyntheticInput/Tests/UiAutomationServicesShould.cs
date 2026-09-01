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
    }
}
