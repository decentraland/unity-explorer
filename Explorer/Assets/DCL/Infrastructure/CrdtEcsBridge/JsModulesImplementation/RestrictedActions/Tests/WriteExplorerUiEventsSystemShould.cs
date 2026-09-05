using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using ECS.TestSuite;
using ECS.Unity.ExplorerUiEvents;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;

namespace CrdtEcsBridge.RestrictedActions.Tests
{
    /// <summary>
    ///     Covers <see cref="WriteExplorerUiEventsSystem" /> turning the queue the restricted actions API fills
    ///     into appended <see cref="PBExplorerUiEventsResult" /> messages on the scene root entity.
    /// </summary>
    public class WriteExplorerUiEventsSystemShould : UnitySystemTestBase<WriteExplorerUiEventsSystem>
    {
        private Queue<ExplorerUiEvent> events = null!;
        private IECSToCRDTWriter ecsToCRDTWriter = null!;
        private ISceneStateProvider sceneStateProvider = null!;
        private List<PBExplorerUiEventsResult> written = null!;

        [SetUp]
        public void SetUp()
        {
            events = new Queue<ExplorerUiEvent>();
            written = new List<PBExplorerUiEventsResult>();
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            // The payload only exists inside the prepare delegate, so capture the delegate and run it the way
            // the real writer would, against a message of its own.
            ecsToCRDTWriter.AppendMessage(
                                Arg.Any<Action<PBExplorerUiEventsResult, (ExplorerUiEvent, uint)>>(),
                                Arg.Any<CRDTEntity>(),
                                Arg.Any<int>(),
                                Arg.Any<(ExplorerUiEvent, uint)>())
                           .Returns(info =>
                            {
                                var result = new PBExplorerUiEventsResult();

                                info.ArgAt<Action<PBExplorerUiEventsResult, (ExplorerUiEvent, uint)>>(0)
                                    .Invoke(result, info.ArgAt<(ExplorerUiEvent, uint)>(3));

                                written.Add(result);

                                return result;
                            });

            system = new WriteExplorerUiEventsSystem(world, events, ecsToCRDTWriter, sceneStateProvider);
        }

        [Test]
        public void WriteAQueuedEventToTheSceneRootEntity()
        {
            // Arrange
            sceneStateProvider.TickNumber.Returns((uint)563);
            events.Enqueue(new ExplorerUiEvent(ExplorerUi.EuMap, ExplorerUiEventKind.Opened));

            // Act
            system.Update(0);

            // Assert
            ecsToCRDTWriter.Received(1)
                           .AppendMessage(
                                Arg.Any<Action<PBExplorerUiEventsResult, (ExplorerUiEvent, uint)>>(),
                                SpecialEntitiesID.SCENE_ROOT_ENTITY,
                                563,
                                (new ExplorerUiEvent(ExplorerUi.EuMap, ExplorerUiEventKind.Opened), (uint)563));

            Assert.That(written, Has.Count.EqualTo(1));
            Assert.That(written[0].Ui, Is.EqualTo(ExplorerUi.EuMap));
            Assert.That(written[0].Timestamp, Is.EqualTo(563u));
            Assert.That(written[0].EventCase, Is.EqualTo(PBExplorerUiEventsResult.EventOneofCase.Opened));
        }

        [Test]
        public void WriteEveryQueuedEventInOrder()
        {
            // Arrange
            sceneStateProvider.TickNumber.Returns((uint)7);
            events.Enqueue(new ExplorerUiEvent(ExplorerUi.EuBackpack, ExplorerUiEventKind.Opened));
            events.Enqueue(new ExplorerUiEvent(ExplorerUi.EuBackpack, ExplorerUiEventKind.Closed));

            // Act
            system.Update(0);

            // Assert
            Assert.That(written, Has.Count.EqualTo(2));
            Assert.That(written[0].EventCase, Is.EqualTo(PBExplorerUiEventsResult.EventOneofCase.Opened));
            Assert.That(written[1].EventCase, Is.EqualTo(PBExplorerUiEventsResult.EventOneofCase.Closed));

            // Both share the tick they were drained on: the set is grow-only and the scene reads it as a
            // window, so a repeated timestamp loses nothing.
            Assert.That(written[0].Timestamp, Is.EqualTo(7u));
            Assert.That(written[1].Timestamp, Is.EqualTo(7u));
        }

        [Test]
        public void WriteNothingOnATickWithoutEvents()
        {
            // Act
            system.Update(0);

            // Assert
            Assert.That(written, Is.Empty);
        }

        [Test]
        public void DrainTheQueueSoNoEventIsWrittenTwice()
        {
            // Arrange
            events.Enqueue(new ExplorerUiEvent(ExplorerUi.EuSettings, ExplorerUiEventKind.Opened));

            // Act
            system.Update(0);
            system.Update(0);

            // Assert
            Assert.That(written, Has.Count.EqualTo(1));
            Assert.That(events, Is.Empty);
        }
    }
}
