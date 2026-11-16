using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.ResetExtensions;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Systems;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utility;
using RaycastHit = DCL.ECSComponents.RaycastHit;

namespace DCL.Interaction.PlayerOriginated.Tests
{
    public class WritePointerEventResultsSystemShould : UnitySystemTestBase<WritePointerEventResultsSystem>
    {
        private const uint TICK = 123;
        private const int UI_ENTITY_ID = 500;

        private readonly List<PBPointerEventsResult> results = new ();
        private readonly List<CRDTEntity> resultEntities = new ();
        private IECSToCRDTWriter writer = null!;
        private IGlobalInputEvents globalInputEvents = null!;
        private ISceneStateProvider sceneStateProvider = null!;

        [SetUp]
        public void SetUp()
        {
            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.Geometry.Returns(new ParcelMathHelper.SceneGeometry(Vector3.zero, new ParcelMathHelper.SceneCircumscribedPlanes(), 0.0f));

            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.TickNumber.Returns(TICK);
            sceneStateProvider.IsCurrent.Returns(true);

            IComponentPool<RaycastHit> pool = Substitute.For<IComponentPool<RaycastHit>>();
            pool.Get().Returns(new RaycastHit().Reset());

            writer = Substitute.For<IECSToCRDTWriter>();

            writer.AppendMessage(
                       Arg.Any<Action<PBPointerEventsResult, (RaycastHit?, InputAction, PointerEventType, uint)>>(),
                       Arg.Any<CRDTEntity>(),
                       Arg.Any<int>(),
                       Arg.Any<(RaycastHit?, InputAction, PointerEventType, uint)>())
                  .Returns(info =>
                   {
                       var res = new PBPointerEventsResult();

                       info.ArgAt<Action<PBPointerEventsResult, (RaycastHit?, InputAction, PointerEventType, uint)>>(0)
                           .Invoke(res, info.ArgAt<(RaycastHit?, InputAction, PointerEventType, uint)>(3));

                       results.Add(res);
                       resultEntities.Add(info.ArgAt<CRDTEntity>(1));

                       return res;
                   });

            system = new WritePointerEventResultsSystem(world, sceneData,
                writer,
                sceneStateProvider,
                globalInputEvents = Substitute.For<IGlobalInputEvents>(),
                pool);

            globalInputEvents.Entries.Returns(new List<IGlobalInputEvents.Entry>());
        }

        [TearDown]
        public void ClearResults()
        {
            results.Clear();
            resultEntities.Clear();
        }

        [Test]
        public void WriteGlobalEvents()
        {
            globalInputEvents.Entries.Returns(new List<IGlobalInputEvents.Entry>
            {
                new (InputAction.IaBackward, PointerEventType.PetUp),
                new (InputAction.IaAction6, PointerEventType.PetDown),
            });

            system.Update(0);

            Assert.That(results.Count, Is.EqualTo(2));
            PBPointerEventsResult first = results[0];
            Assert.That(first.Button, Is.EqualTo(InputAction.IaBackward));
            Assert.That(first.State, Is.EqualTo(PointerEventType.PetUp));
            Assert.That(first.TickNumber, Is.EqualTo(TICK));
            Assert.That(first.Timestamp, Is.EqualTo(TICK));
            Assert.That(first.Hit, Is.Null);

            PBPointerEventsResult second = results[1];
            Assert.That(second.Button, Is.EqualTo(InputAction.IaAction6));
            Assert.That(second.State, Is.EqualTo(PointerEventType.PetDown));
            Assert.That(second.TickNumber, Is.EqualTo(TICK));
            Assert.That(second.Timestamp, Is.EqualTo(TICK));
            Assert.That(second.Hit, Is.Null);

            // Releasing IaBackward (movement) is not a gesture, pressing IaAction6 is
            sceneStateProvider.Received(1).LastUserInputTick = TICK;
        }

        [Test]
        public void NotRecordUserGestureForMovementInput()
        {
            globalInputEvents.Entries.Returns(new List<IGlobalInputEvents.Entry>
            {
                new (InputAction.IaForward, PointerEventType.PetDown),
                new (InputAction.IaJump, PointerEventType.PetDown),
                new (InputAction.IaWalk, PointerEventType.PetUp),
                new (InputAction.IaAny, PointerEventType.PetDown),
            });

            system.Update(0);

            sceneStateProvider.DidNotReceive().LastUserInputTick = Arg.Any<uint>();
        }

        [Test]
        [TestCase(PointerEventType.PetProximityEnter)]
        [TestCase(PointerEventType.PetProximityLeave)]
        public void NotRecordUserGestureForProximityEvents(PointerEventType eventType)
        {
            var sdkEvents = new PBPointerEvents
            {
                AppendPointerEventResultsIntent = new AppendPointerEventResultsIntent(),
                PointerEvents = { CreateEntry(eventType, InputAction.IaPointer) },
            };

            sdkEvents.AppendPointerEventResultsIntent.InitializeWithAlloc();
            sdkEvents.AppendPointerEventResultsIntent.AddValidIndex(0);
            world.Create(new CRDTEntity(100), sdkEvents);

            system.Update(0);

            sceneStateProvider.DidNotReceive().LastUserInputTick = Arg.Any<uint>();
        }

        [Test]
        public void WriteValidResults()
        {
            var sdkEvents = new PBPointerEvents
            {
                AppendPointerEventResultsIntent = new AppendPointerEventResultsIntent(),
                PointerEvents =
                {
                    CreateEntry(PointerEventType.PetHoverEnter, InputAction.IaPointer),
                    CreateEntry(PointerEventType.PetHoverLeave, InputAction.IaForward),
                    CreateEntry(PointerEventType.PetUp, InputAction.IaAction3),
                    CreateEntry(PointerEventType.PetDown, InputAction.IaPrimary),
                },
            };

            sdkEvents.AppendPointerEventResultsIntent.InitializeWithAlloc();
            sdkEvents.AppendPointerEventResultsIntent.AddValidIndex(1);
            sdkEvents.AppendPointerEventResultsIntent.AddValidIndex(3);

            var sdkEntity = new CRDTEntity(100);

            world.Create(sdkEntity, sdkEvents);

            system.Update(0);

            AssertMessageAppended(1, 0);
            AssertMessageAppended(3, 1);

            void AssertMessageAppended(int originalIndex, uint counter)
            {
                PBPointerEvents.Types.Entry entry = sdkEvents.PointerEvents[originalIndex];
                PBPointerEventsResult r = results[(int)counter];

                Assert.That(r.Button, Is.EqualTo(entry.EventType is PointerEventType.PetHoverEnter or PointerEventType.PetHoverLeave ? InputAction.IaPointer : entry.EventInfo.Button));
                Assert.That(r.State, Is.EqualTo(entry.EventType));
                Assert.That(r.TickNumber, Is.EqualTo(TICK));
                Assert.That(r.Timestamp, Is.EqualTo(TICK));
                Assert.That(r.Hit, Is.Not.Null);
            }

            Assert.That(writer.ReceivedCalls().Count(), Is.EqualTo(2));

            // Only the PetDown result is a gesture, the PetHoverLeave one is not
            sceneStateProvider.Received(1).LastUserInputTick = TICK;
        }

        [Test]
        public void WriteGlobalEventsOnlyForCurrentScene()
        {
            sceneStateProvider.IsCurrent.Returns(false);

            globalInputEvents.Entries.Returns(new List<IGlobalInputEvents.Entry>
            {
                new (InputAction.IaBackward, PointerEventType.PetUp),
                new (InputAction.IaAction6, PointerEventType.PetDown),
            });

            system.Update(0);

            Assert.That(results.Count, Is.EqualTo(0));

            sceneStateProvider.IsCurrent.Returns(true);

            system.Update(0);

            Assert.That(results.Count, Is.EqualTo(2));
        }

        [Test]
        public void WriteEveryQueuedUiPointerEventInOrder()
        {
            // Arrange
            UITransformComponent uiTransform = CreateUiEntity(PointerEventType.PetDown, PointerEventType.PetUp);
            uiTransform.PendingPointerEvents.Add(PointerEventType.PetDown);
            uiTransform.PendingPointerEvents.Add(PointerEventType.PetUp);

            // Act
            system.Update(0);

            // Assert
            Assert.That(results.Select(r => r.State), Is.EqualTo(new[] { PointerEventType.PetDown, PointerEventType.PetUp }));
            Assert.That(results.Select(r => r.Button), Is.All.EqualTo(InputAction.IaPointer));
            Assert.That(results.Select(r => r.Timestamp), Is.All.EqualTo(TICK));
            Assert.That(results.Select(r => r.Hit), Is.All.Null);
            Assert.That(resultEntities, Is.All.EqualTo(new CRDTEntity(UI_ENTITY_ID)));
            Assert.That(uiTransform.PendingPointerEvents, Is.Empty);
        }

        [Test]
        public void SkipQueuedUiPointerEventsTheEntityDoesNotListenTo()
        {
            // Arrange
            UITransformComponent uiTransform = CreateUiEntity(PointerEventType.PetDown);
            uiTransform.PendingPointerEvents.Add(PointerEventType.PetHoverEnter);
            uiTransform.PendingPointerEvents.Add(PointerEventType.PetDown);
            uiTransform.PendingPointerEvents.Add(PointerEventType.PetHoverLeave);

            // Act
            system.Update(0);

            // Assert
            Assert.That(results.Select(r => r.State), Is.EqualTo(new[] { PointerEventType.PetDown }));
            Assert.That(uiTransform.PendingPointerEvents, Is.Empty);
        }

        [Test]
        public void CountOnlyTheAppendedUiResultsInTheElementsDrainLog()
        {
            // Arrange - the entity listens to the press only
            UITransformComponent uiTransform = CreateUiEntity(PointerEventType.PetDown);
            uiTransform.PendingPointerEvents.Add(PointerEventType.PetHoverEnter);
            uiTransform.PendingPointerEvents.Add(PointerEventType.PetDown);
            uiTransform.PendingPointerEvents.Add(PointerEventType.PetDown);

            // Act
            system.Update(0);

            // Assert
            Assert.That(uiTransform.Drains.AppendedCount(PointerEventType.PetDown), Is.EqualTo(2));
            Assert.That(uiTransform.Drains.AppendedCount(PointerEventType.PetHoverEnter), Is.Zero);
            Assert.That(uiTransform.Drains.AppendedCount(PointerEventType.PetUp), Is.Zero);
        }

        [Test]
        public void WriteUiAndGlobalEventsOfTheSameFrameWithTheSameTick()
        {
            // Arrange - the tick advances on every read, so a shared stamp proves it was read once
            sceneStateProvider.TickNumber.Returns(TICK, TICK + 1, TICK + 2);
            UITransformComponent uiTransform = CreateUiEntity(PointerEventType.PetDown);
            uiTransform.PendingPointerEvents.Add(PointerEventType.PetDown);
            globalInputEvents.Entries.Returns(new List<IGlobalInputEvents.Entry> { new (InputAction.IaPointer, PointerEventType.PetDown) });

            // Act
            system.Update(0);

            // Assert
            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results.Select(r => r.Timestamp), Is.All.EqualTo(TICK));
            Assert.That(resultEntities, Is.EquivalentTo(new[] { new CRDTEntity(UI_ENTITY_ID), new CRDTEntity(SpecialEntitiesID.SCENE_ROOT_ENTITY) }));
        }

        [Test]
        [TestCase(PointerEventType.PetDown, true)]
        [TestCase(PointerEventType.PetUp, true)]
        [TestCase(PointerEventType.PetHoverEnter, false)]
        [TestCase(PointerEventType.PetHoverLeave, false)]
        public void CountOnlyUiPressAndReleaseAsUserGesture(PointerEventType eventType, bool isUserGesture)
        {
            // Arrange
            UITransformComponent uiTransform = CreateUiEntity(eventType);
            uiTransform.PendingPointerEvents.Add(eventType);

            // Act
            system.Update(0);

            // Assert
            Assert.That(results.Count, Is.EqualTo(1));

            if (isUserGesture)
                sceneStateProvider.Received(1).LastUserInputTick = TICK;
            else
                sceneStateProvider.DidNotReceive().LastUserInputTick = Arg.Any<uint>();
        }

        [Test]
        public void WriteOnlyUiEventsWhenTheSceneIsNotCurrent()
        {
            // Arrange
            sceneStateProvider.IsCurrent.Returns(false);
            UITransformComponent uiTransform = CreateUiEntity(PointerEventType.PetDown);
            uiTransform.PendingPointerEvents.Add(PointerEventType.PetDown);
            globalInputEvents.Entries.Returns(new List<IGlobalInputEvents.Entry> { new (InputAction.IaPointer, PointerEventType.PetDown) });

            // Act
            system.Update(0);

            // Assert
            Assert.That(results.Select(r => r.State), Is.EqualTo(new[] { PointerEventType.PetDown }));
            Assert.That(resultEntities, Is.EqualTo(new[] { new CRDTEntity(UI_ENTITY_ID) }));
            Assert.That(uiTransform.PendingPointerEvents, Is.Empty);
        }

        private UITransformComponent CreateUiEntity(params PointerEventType[] listenedEventTypes)
        {
            var pbPointerEvents = new PBPointerEvents();

            foreach (PointerEventType eventType in listenedEventTypes)
                pbPointerEvents.PointerEvents.Add(CreateEntry(eventType, InputAction.IaPointer));

            Entity entity = world.Create(new CRDTEntity(UI_ENTITY_ID), pbPointerEvents);
            return AddUITransformToEntity(entity);
        }

        private static PBPointerEvents.Types.Entry CreateEntry(PointerEventType eventType, InputAction button) =>
            new ()
            {
                EventType = eventType,
                EventInfo = new PBPointerEvents.Types.Info
                {
                    Button = button,
                    MaxDistance = 100,
                },
            };
    }
}
