using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using ECS.TestSuite;
using ECS.Unity.ExplorerUiEvents;
using NSubstitute;
using NUnit.Framework;
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
        private const string ITEM_URN = "urn:decentraland:matic:collections-v2:0x2222222222222222222222222222222222222222:3";

        private Queue<ExplorerUiEvent> events = null!;
        private Queue<ExplorerItemPurchaseEvent> itemPurchaseEvents = null!;
        private IECSToCRDTWriter ecsToCRDTWriter = null!;
        private List<PBExplorerUiEventsResult> written = null!;
        private List<PBExplorerItemPurchaseResult> writtenPurchases = null!;

        // Every message of both components, in the order the system appended them.
        private List<object> writtenInOrder = null!;

        [SetUp]
        public void SetUp()
        {
            events = new Queue<ExplorerUiEvent>();
            itemPurchaseEvents = new Queue<ExplorerItemPurchaseEvent>();
            written = new List<PBExplorerUiEventsResult>();
            writtenPurchases = new List<PBExplorerItemPurchaseResult>();
            writtenInOrder = new List<object>();
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            // The payload only exists inside the prepare delegate, so capture the delegate and run it the way
            // the real writer would, against a message of its own.
            ecsToCRDTWriter.AppendMessage(
                                Arg.Any<Action<PBExplorerUiEventsResult, ExplorerUiEvent>>(),
                                Arg.Any<CRDTEntity>(),
                                Arg.Any<int>(),
                                Arg.Any<ExplorerUiEvent>())
                           .Returns(info =>
                            {
                                var result = new PBExplorerUiEventsResult();

                                info.ArgAt<Action<PBExplorerUiEventsResult, ExplorerUiEvent>>(0)
                                    .Invoke(result, info.ArgAt<ExplorerUiEvent>(3));

                                written.Add(result);
                                writtenInOrder.Add(result);

                                return result;
                            });

            ecsToCRDTWriter.AppendMessage(
                                Arg.Any<Action<PBExplorerItemPurchaseResult, ExplorerItemPurchaseEvent>>(),
                                Arg.Any<CRDTEntity>(),
                                Arg.Any<int>(),
                                Arg.Any<ExplorerItemPurchaseEvent>())
                           .Returns(info =>
                            {
                                var result = new PBExplorerItemPurchaseResult();

                                info.ArgAt<Action<PBExplorerItemPurchaseResult, ExplorerItemPurchaseEvent>>(0)
                                    .Invoke(result, info.ArgAt<ExplorerItemPurchaseEvent>(3));

                                writtenPurchases.Add(result);
                                writtenInOrder.Add(result);

                                return result;
                            });

            system = new WriteExplorerUiEventsSystem(world, events, itemPurchaseEvents, ecsToCRDTWriter);
        }

        [Test]
        public void WriteAQueuedEventToTheSceneRootEntity()
        {
            // Arrange
            var uiEvent = new ExplorerUiEvent(ExplorerUi.EuMap, ExplorerUiEventKind.Opened, 12, 563);
            events.Enqueue(uiEvent);

            // Act
            system.Update(0);

            // Assert
            ecsToCRDTWriter.Received(1)
                           .AppendMessage(
                                Arg.Any<Action<PBExplorerUiEventsResult, ExplorerUiEvent>>(),
                                SpecialEntitiesID.SCENE_ROOT_ENTITY,
                                563,
                                uiEvent);

            Assert.That(written, Has.Count.EqualTo(1));
            Assert.That(written[0].Ui, Is.EqualTo(ExplorerUi.EuMap));
            Assert.That(written[0].Timestamp, Is.EqualTo(563u));
            Assert.That(written[0].RequestId, Is.EqualTo(12u));
            Assert.That(written[0].EventCase, Is.EqualTo(PBExplorerUiEventsResult.EventOneofCase.Opened));
        }

        [Test]
        public void KeepTheTickAndIdEachEventWasQueuedWith()
        {
            // Arrange: two calls on the same panel, opened a tick apart, drained together.
            events.Enqueue(new ExplorerUiEvent(ExplorerUi.EuBackpack, ExplorerUiEventKind.Opened, 1, 7));
            events.Enqueue(new ExplorerUiEvent(ExplorerUi.EuBackpack, ExplorerUiEventKind.Closed, 1, 9));
            events.Enqueue(new ExplorerUiEvent(ExplorerUi.EuBackpack, ExplorerUiEventKind.Opened, 2, 9));

            // Act
            system.Update(0);

            // Assert
            Assert.That(written, Has.Count.EqualTo(3));
            Assert.That(written[0].EventCase, Is.EqualTo(PBExplorerUiEventsResult.EventOneofCase.Opened));
            Assert.That(written[1].EventCase, Is.EqualTo(PBExplorerUiEventsResult.EventOneofCase.Closed));

            // Sharing a drain would flatten these onto one tick and lose which call each belongs to.
            Assert.That(written.ConvertAll(static result => (result.Timestamp, result.RequestId)),
                Is.EqualTo(new[] { (7u, 1u), (9u, 1u), (9u, 2u) }));
        }

        [TestCase(ExplorerItemPurchaseStatus.Purchased, PBExplorerItemPurchaseResult.StatusOneofCase.Purchased)]
        [TestCase(ExplorerItemPurchaseStatus.Dismissed, PBExplorerItemPurchaseResult.StatusOneofCase.Dismissed)]
        [TestCase(ExplorerItemPurchaseStatus.Failed, PBExplorerItemPurchaseResult.StatusOneofCase.Failed)]
        public void WriteAPurchaseOutcomeToTheSceneRootEntity(ExplorerItemPurchaseStatus status, PBExplorerItemPurchaseResult.StatusOneofCase expectedCase)
        {
            // Arrange
            var purchaseEvent = new ExplorerItemPurchaseEvent(ITEM_URN, status, 4, 90);
            itemPurchaseEvents.Enqueue(purchaseEvent);

            // Act
            system.Update(0);

            // Assert
            ecsToCRDTWriter.Received(1)
                           .AppendMessage(
                                Arg.Any<Action<PBExplorerItemPurchaseResult, ExplorerItemPurchaseEvent>>(),
                                SpecialEntitiesID.SCENE_ROOT_ENTITY,
                                90,
                                purchaseEvent);

            Assert.That(writtenPurchases, Has.Count.EqualTo(1));
            Assert.That(writtenPurchases[0].Urn, Is.EqualTo(ITEM_URN));
            Assert.That(writtenPurchases[0].Timestamp, Is.EqualTo(90u));
            Assert.That(writtenPurchases[0].RequestId, Is.EqualTo(4u));
            Assert.That(writtenPurchases[0].StatusCase, Is.EqualTo(expectedCase));
            Assert.That(itemPurchaseEvents, Is.Empty);
        }

        [Test]
        public void WriteAPurchaseOutcomeBeforeTheClosedEventOfItsModal()
        {
            // Arrange
            events.Enqueue(new ExplorerUiEvent(ExplorerUi.EuItemPurchase, ExplorerUiEventKind.Closed, 4, 90));
            itemPurchaseEvents.Enqueue(new ExplorerItemPurchaseEvent(ITEM_URN, ExplorerItemPurchaseStatus.Purchased, 4, 90));

            // Act
            system.Update(0);

            // Assert
            Assert.That(writtenInOrder, Has.Count.EqualTo(2));
            Assert.That(writtenInOrder[0], Is.InstanceOf<PBExplorerItemPurchaseResult>());
            Assert.That(writtenInOrder[1], Is.InstanceOf<PBExplorerUiEventsResult>());
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
            events.Enqueue(new ExplorerUiEvent(ExplorerUi.EuSettings, ExplorerUiEventKind.Opened, 0, 1));

            // Act
            system.Update(0);
            system.Update(0);

            // Assert
            Assert.That(written, Has.Count.EqualTo(1));
            Assert.That(events, Is.Empty);
        }
    }
}
