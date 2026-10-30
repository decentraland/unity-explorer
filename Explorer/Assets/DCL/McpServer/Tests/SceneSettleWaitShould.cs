using Cysharp.Threading.Tasks;
using DCL.McpServer.Utils;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Threading;
using Utility.Multithreading;

namespace DCL.McpServer.Tests
{
    public class SceneSettleWaitShould
    {
        private IScenesCache scenesCache = null!;
        private ISceneFacade previous = null!;
        private ReactiveProperty<ISceneFacade?> currentScene = null!;
        private uint tick;
        private int framesYielded;

        [SetUp]
        public void SetUp()
        {
            previous = Substitute.For<ISceneFacade>();
            currentScene = new ReactiveProperty<ISceneFacade?>(previous);

            scenesCache = Substitute.For<IScenesCache>();
            scenesCache.CurrentScene.Returns(currentScene);

            tick = 0;
            framesYielded = 0;
        }

        [Test]
        public void SettleOnceADifferentRunningFacadeTickedEnough()
        {
            // Arrange
            currentScene.Value = FreshScene(SceneState.Running);

            // Act
            SceneSettleWait.Result result = Wait(minTicks: 2, deadlineSec: 10f);

            // Assert
            Assert.That(result.Settled, Is.True);
            Assert.That(result.State, Is.EqualTo(SceneState.Running));
            Assert.That(result.TicksWaited, Is.EqualTo(2u));
            Assert.That(framesYielded, Is.EqualTo(2));
        }

        [Test]
        public void SettleWithoutYieldingWhenNoTicksAreRequired()
        {
            // Arrange
            currentScene.Value = FreshScene(SceneState.Running);

            // Act
            SceneSettleWait.Result result = Wait(minTicks: 0, deadlineSec: 10f);

            // Assert
            Assert.That(result.Settled, Is.True);
            Assert.That(framesYielded, Is.EqualTo(0));
        }

        [Test]
        public void StopEarlyOnAnErrorState()
        {
            // Arrange
            currentScene.Value = FreshScene(SceneState.JavaScriptError);

            // Act
            SceneSettleWait.Result result = Wait(minTicks: 2, deadlineSec: 10f);

            // Assert
            Assert.That(result.Settled, Is.False);
            Assert.That(result.State, Is.EqualTo(SceneState.JavaScriptError));
            Assert.That(framesYielded, Is.EqualTo(0));
        }

        [Test]
        public void ReportUnsettledAtTheDeadlineWhileThePreviousSceneIsStillCurrent()
        {
            // Act — the current scene never changes, so the wait can only give up.
            SceneSettleWait.Result result = Wait(minTicks: 2, deadlineSec: 0f);

            // Assert
            Assert.That(result.Settled, Is.False);
            Assert.That(result.TicksWaited, Is.EqualTo(0u));
            Assert.That(framesYielded, Is.EqualTo(0));
        }

        [Test]
        public void CountTicksFromTheMomentTheFreshSceneAppeared()
        {
            // Arrange — the fresh scene is already at tick 40 when it becomes current.
            tick = 40;
            currentScene.Value = FreshScene(SceneState.Running);

            // Act
            SceneSettleWait.Result result = Wait(minTicks: 3, deadlineSec: 10f);

            // Assert
            Assert.That(result.Settled, Is.True);
            Assert.That(result.TicksWaited, Is.EqualTo(3u));
        }

        private ISceneFacade FreshScene(SceneState state)
        {
            ISceneStateProvider stateProvider = Substitute.For<ISceneStateProvider>();
            stateProvider.State.Returns(new Atomic<SceneState>(state));
            stateProvider.TickNumber.Returns(_ => tick);

            ISceneFacade fresh = Substitute.For<ISceneFacade>();
            fresh.SceneStateProvider.Returns(stateProvider);
            return fresh;
        }

        /// <summary>Drives the poll loop synchronously: each yielded frame advances the scene by one tick.</summary>
        private SceneSettleWait.Result Wait(int minTicks, float deadlineSec)
        {
            UniTask<SceneSettleWait.Result> wait = SceneSettleWait.WaitForFreshSceneAsync(scenesCache, previous, minTicks, deadlineSec, CancellationToken.None, _ =>
            {
                framesYielded++;
                tick++;
                return UniTask.CompletedTask;
            });

            Assert.That(wait.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            return wait.GetAwaiter().GetResult();
        }
    }
}
