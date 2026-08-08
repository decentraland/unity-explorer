using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated;
using DCL.Ipfs;
using DCL.McpServer.Components;
using DCL.McpServer.Systems;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.McpServer.Tests
{
    public class McpInputActionSystemShould : UnitySystemTestBase<McpInputActionSystem>
    {
        /// <summary>The three ways the current scene can be unusable, which the system rejects identically.</summary>
        public enum SceneGuard
        {
            ABSENT,
            NOT_CURRENT,
            NOT_RUNNING,
        }

        private Entity playerEntity;
        private GlobalInputEvents globalInputEvents = null!;
        private ISceneStateProvider sceneStateProvider = null!;
        private IReadonlyReactiveProperty<ISceneFacade?> currentScene = null!;
        private uint tick;

        [SetUp]
        public void SetUp()
        {
            playerEntity = world.Create();
            globalInputEvents = new GlobalInputEvents();

            tick = 100u;
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);
            sceneStateProvider.State.Returns(new Atomic<SceneState>(SceneState.Running));
            sceneStateProvider.TickNumber.Returns(_ => tick);

            ISceneFacade sceneFacade = Substitute.For<ISceneFacade>();
            sceneFacade.SceneStateProvider.Returns(sceneStateProvider);
            sceneFacade.SceneData.SceneEntityDefinition.Returns(new SceneEntityDefinition("scene-here", new SceneMetadata()));

            currentScene = Substitute.For<IReadonlyReactiveProperty<ISceneFacade?>>();
            currentScene.Value.Returns(sceneFacade);

            IScenesCache scenesCache = Substitute.For<IScenesCache>();
            scenesCache.CurrentScene.Returns(currentScene);

            system = new McpInputActionSystem(world, scenesCache, globalInputEvents, playerEntity);
        }

        [TestCase(null, PointerEventType.PetDown)]
        [TestCase(null, PointerEventType.PetUp)]
        [TestCase("scene-here", PointerEventType.PetDown)]
        public void PublishALoneEdgeAndCompleteImmediately(string? sceneId, PointerEventType eventType)
        {
            // Arrange
            UniTaskCompletionSource<McpInputActionResult> completion = AddIntent(eventType, sceneId: sceneId);

            // Act
            system!.Update(0);

            // Assert
            AssertPublished(0, eventType);
            McpInputActionResult result = ResultOf(completion);
            Assert.That(result.Delivered, Is.True);
            Assert.That(result.SceneId, Is.EqualTo("scene-here"));
            Assert.That(result.ReleaseMissed, Is.False);
            Assert.That(world.Has<McpInputActionIntent>(playerEntity), Is.False);
        }

        [Test]
        public void WithholdTheReleaseUntilBothTheHoldAndTheSceneTickHaveMovedOn()
        {
            // A press and its release sharing one scene tick collapse into an ambiguous button state in the SDK,
            // which keys pointer results by that tick — so an elapsed hold alone must not let the release out.

            // Arrange
            UniTaskCompletionSource<McpInputActionResult> completion = AddIntent(PointerEventType.PetDown, holdSeconds: 60f);

            // Act
            system!.Update(0); // publish the press
            system.Update(0); // stamp the tick the release is gated against
            tick++;
            system.Update(0); // the tick gate is open, the hold is not

            // Assert
            Assert.That(globalInputEvents.Entries.Count, Is.EqualTo(1));
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(world.Has<McpInputActionIntent>(playerEntity), Is.True);

            // Act: back the press time off so the hold is over, and put the scene back on the press tick.
            world.Get<McpInputActionIntent>(playerEntity).PressTime = UnityEngine.Time.time - 61f;
            tick--;
            system.Update(0);

            // Assert
            Assert.That(globalInputEvents.Entries.Count, Is.EqualTo(1), "the release must not share the press tick");
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Pending));

            // Act
            tick++;
            system.Update(0);

            // Assert
            AssertPublished(1, PointerEventType.PetUp);
            McpInputActionResult result = ResultOf(completion);
            Assert.That(result.Delivered, Is.True);
            Assert.That(result.ReleaseMissed, Is.False);
            Assert.That(result.HeldSeconds, Is.GreaterThan(60f));
            Assert.That(world.Has<McpInputActionIntent>(playerEntity), Is.False);
        }

        [Test]
        public void ReportAPressWhoseReleaseTheSceneGuardRejected()
        {
            // Arrange
            UniTaskCompletionSource<McpInputActionResult> completion = AddIntent(PointerEventType.PetDown, holdSeconds: 0f);

            system!.Update(0);
            system.Update(0);

            // Act
            sceneStateProvider.IsCurrent.Returns(false);
            tick++;
            system.Update(0);

            // Assert
            Assert.That(globalInputEvents.Entries.Count, Is.EqualTo(1), "only the press reached the scene");
            McpInputActionResult result = ResultOf(completion);
            Assert.That(result.Delivered, Is.True);
            Assert.That(result.ReleaseMissed, Is.True);
            Assert.That(result.FailureReason, Does.Contain("no running current scene"));
        }

        [TestCase(SceneGuard.ABSENT)]
        [TestCase(SceneGuard.NOT_CURRENT)]
        [TestCase(SceneGuard.NOT_RUNNING)]
        public void FailWhenThereIsNoRunningCurrentScene(SceneGuard guard)
        {
            // Arrange
            switch (guard)
            {
                case SceneGuard.ABSENT:
                    currentScene.Value.Returns((ISceneFacade?)null);
                    break;
                case SceneGuard.NOT_CURRENT:
                    sceneStateProvider.IsCurrent.Returns(false);
                    break;
                case SceneGuard.NOT_RUNNING:
                    sceneStateProvider.State.Returns(new Atomic<SceneState>(SceneState.JavaScriptError));
                    break;
            }

            UniTaskCompletionSource<McpInputActionResult> completion = AddIntent(PointerEventType.PetDown);

            // Act
            system!.Update(0);

            // Assert
            Assert.That(globalInputEvents.Entries, Is.Empty);
            McpInputActionResult result = ResultOf(completion);
            Assert.That(result.Delivered, Is.False);
            Assert.That(result.ReleaseMissed, Is.False);
            Assert.That(result.FailureReason, Does.Contain("no running current scene"));
        }

        [Test]
        public void FailWhenPinnedSceneIsNotCurrent()
        {
            // Arrange
            UniTaskCompletionSource<McpInputActionResult> completion = AddIntent(PointerEventType.PetDown, sceneId: "scene-elsewhere");

            // Act
            system!.Update(0);

            // Assert
            Assert.That(globalInputEvents.Entries, Is.Empty);
            Assert.That(ResultOf(completion).FailureReason, Does.Contain("pinned"));
        }

        [Test]
        public void LeaveTheBufferAloneWhenNoRequestIsPending()
        {
            // The buffer belongs to the real input pipeline; an idle update of this system must not touch it.

            // Arrange
            globalInputEvents.Add(new IGlobalInputEvents.Entry(InputAction.IaJump, PointerEventType.PetDown));

            // Act
            system!.Update(0);

            // Assert
            Assert.That(globalInputEvents.Entries.Count, Is.EqualTo(1));
        }

        private UniTaskCompletionSource<McpInputActionResult> AddIntent(
            PointerEventType eventType,
            float? holdSeconds = null,
            string? sceneId = null)
        {
            var completion = new UniTaskCompletionSource<McpInputActionResult>();

            world.Add(playerEntity, new McpInputActionIntent(InputAction.IaAction5, sceneId, eventType, holdSeconds)
            {
                Completion = completion,
            });

            return completion;
        }

        private void AssertPublished(int index, PointerEventType eventType)
        {
            IReadOnlyList<IGlobalInputEvents.Entry> entries = globalInputEvents.Entries;
            Assert.That(entries.Count, Is.GreaterThan(index));
            Assert.That(entries[index].InputAction, Is.EqualTo(InputAction.IaAction5));
            Assert.That(entries[index].PointerEventType, Is.EqualTo(eventType));
        }

        private static McpInputActionResult ResultOf(UniTaskCompletionSource<McpInputActionResult> completion)
        {
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            return completion.Task.GetAwaiter().GetResult();
        }
    }
}
