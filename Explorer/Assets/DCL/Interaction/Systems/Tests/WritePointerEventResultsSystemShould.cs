using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated;
using DCL.Interaction.PlayerOriginated.Systems;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using RaycastHit = DCL.ECSComponents.RaycastHit;

namespace DCL.Interaction.Systems.Tests
{
    public class WritePointerEventResultsSystemShould : UnitySystemTestBase<WritePointerEventResultsSystem>
    {
        private const uint TICK = 123;
        private const int UI_ENTITY_ID = 500;

        private ISceneStateProvider sceneStateProvider = null!;
        private IECSToCRDTWriter ecsToCRDTWriter = null!;
        private List<IGlobalInputEvents.Entry> globalEntries = null!;
        private List<(CRDTEntity entity, InputAction button, PointerEventType eventType, uint tick)> written = null!;

        private Entity uiEntity;
        private UITransformComponent uiTransformComponent = null!;

        [SetUp]
        public void SetUp()
        {
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);
            sceneStateProvider.TickNumber.Returns(TICK);

            globalEntries = new List<IGlobalInputEvents.Entry>();
            IGlobalInputEvents globalInputEvents = Substitute.For<IGlobalInputEvents>();
            globalInputEvents.Entries.Returns(globalEntries);

            written = new List<(CRDTEntity, InputAction, PointerEventType, uint)>();
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            ecsToCRDTWriter.AppendMessage(
                                 Arg.Any<Action<PBPointerEventsResult, (RaycastHit? sdkHit, InputAction button, PointerEventType eventType, uint tick)>>(),
                                 Arg.Any<CRDTEntity>(),
                                 Arg.Any<int>(),
                                 Arg.Any<(RaycastHit? sdkHit, InputAction button, PointerEventType eventType, uint tick)>())
                            .Returns(call =>
                             {
                                 var result = new PBPointerEventsResult();
                                 var data = call.ArgAt<(RaycastHit? sdkHit, InputAction button, PointerEventType eventType, uint tick)>(3);
                                 call.ArgAt<Action<PBPointerEventsResult, (RaycastHit? sdkHit, InputAction button, PointerEventType eventType, uint tick)>>(0)(result, data);
                                 written.Add((call.ArgAt<CRDTEntity>(1), result.Button, result.State, result.Timestamp));
                                 return result;
                             });

            system = new WritePointerEventResultsSystem(world, Substitute.For<ISceneData>(), ecsToCRDTWriter, sceneStateProvider, globalInputEvents,
                Substitute.For<IComponentPool<RaycastHit>>());

            uiEntity = world.Create();
            uiTransformComponent = AddUITransformToEntity(uiEntity);
            world.Add(uiEntity, new CRDTEntity(UI_ENTITY_ID), CreatePointerEvents(PointerEventType.PetDown, PointerEventType.PetUp));
        }

        [Test]
        public void WriteEveryQueuedUiPointerEventInOrder()
        {
            // Arrange
            uiTransformComponent.PendingPointerEvents.Add(PointerEventType.PetDown);
            uiTransformComponent.PendingPointerEvents.Add(PointerEventType.PetUp);

            // Act
            system.Update(0);

            // Assert
            CollectionAssert.AreEqual(
                new[]
                {
                    (new CRDTEntity(UI_ENTITY_ID), InputAction.IaPointer, PointerEventType.PetDown, TICK),
                    (new CRDTEntity(UI_ENTITY_ID), InputAction.IaPointer, PointerEventType.PetUp, TICK),
                },
                written);

            Assert.IsEmpty(uiTransformComponent.PendingPointerEvents);
        }

        [Test]
        public void SkipQueuedUiPointerEventsTheEntityDoesNotListenTo()
        {
            // Arrange
            uiTransformComponent.PendingPointerEvents.Add(PointerEventType.PetHoverEnter);
            uiTransformComponent.PendingPointerEvents.Add(PointerEventType.PetDown);
            uiTransformComponent.PendingPointerEvents.Add(PointerEventType.PetHoverLeave);

            // Act
            system.Update(0);

            // Assert
            CollectionAssert.AreEqual(new[] { (new CRDTEntity(UI_ENTITY_ID), InputAction.IaPointer, PointerEventType.PetDown, TICK) }, written);
            Assert.IsEmpty(uiTransformComponent.PendingPointerEvents);
        }

        [Test]
        public void WriteUiAndGlobalEventsOfTheSameFrameWithTheSameTick()
        {
            // Arrange
            uiTransformComponent.PendingPointerEvents.Add(PointerEventType.PetDown);
            globalEntries.Add(new IGlobalInputEvents.Entry(InputAction.IaPointer, PointerEventType.PetDown));

            // Act
            system.Update(0);

            // Assert
            CollectionAssert.AreEquivalent(
                new[]
                {
                    (new CRDTEntity(UI_ENTITY_ID), InputAction.IaPointer, PointerEventType.PetDown, TICK),
                    (new CRDTEntity(SpecialEntitiesID.SCENE_ROOT_ENTITY), InputAction.IaPointer, PointerEventType.PetDown, TICK),
                },
                written);
        }

        [Test]
        [TestCase(PointerEventType.PetDown, true)]
        [TestCase(PointerEventType.PetUp, true)]
        [TestCase(PointerEventType.PetHoverEnter, false)]
        [TestCase(PointerEventType.PetHoverLeave, false)]
        public void CountOnlyPressAndReleaseAsUserGesture(PointerEventType eventType, bool isUserGesture)
        {
            // Arrange
            world.Set(uiEntity, CreatePointerEvents(eventType));
            uiTransformComponent.PendingPointerEvents.Add(eventType);

            // Act
            system.Update(0);

            // Assert
            Assert.AreEqual(1, written.Count);

            if (isUserGesture)
                sceneStateProvider.Received(1).LastUserInputTick = TICK;
            else
                sceneStateProvider.DidNotReceive().LastUserInputTick = Arg.Any<uint>();
        }

        [Test]
        public void WriteNothingWhenTheSceneIsNotCurrent()
        {
            // Arrange
            sceneStateProvider.IsCurrent.Returns(false);
            uiTransformComponent.PendingPointerEvents.Add(PointerEventType.PetDown);
            globalEntries.Add(new IGlobalInputEvents.Entry(InputAction.IaPointer, PointerEventType.PetDown));

            // Act
            system.Update(0);

            // Assert
            Assert.IsEmpty(written);
            Assert.AreEqual(1, uiTransformComponent.PendingPointerEvents.Count);
        }

        private static PBPointerEvents CreatePointerEvents(params PointerEventType[] eventTypes)
        {
            var pbPointerEvents = new PBPointerEvents();

            foreach (PointerEventType eventType in eventTypes)
            {
                pbPointerEvents.PointerEvents.Add(new PBPointerEvents.Types.Entry
                {
                    EventType = eventType,
                    EventInfo = new PBPointerEvents.Types.Info { Button = InputAction.IaPointer },
                });
            }

            return pbPointerEvents;
        }
    }
}
