using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UIPointerEvents;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using UnityEngine.UIElements;
using Entity = Arch.Core.Entity;
using RaycastHit = DCL.ECSComponents.RaycastHit;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UIPointerEventsSystemShould : UnitySystemTestBase<UIPointerEventsSystem>
    {
        private Entity entity;
        private UITransformComponent uiTransformComponent = null!;
        private ISceneStateProvider sceneStateProvider = null!;
        private IECSToCRDTWriter ecsToCRDTWriter = null!;

        [SetUp]
        public void SetUp()
        {
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.TickNumber.Returns(123u);
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            system = new UIPointerEventsSystem(world, sceneStateProvider, ecsToCRDTWriter);
            entity = world.Create();
            uiTransformComponent = AddUITransformToEntity(entity);
            world.Add(entity, new CRDTEntity(500));
        }

        [Test]
        [TestCase(PointerEventType.PetDown, true)]
        [TestCase(PointerEventType.PetUp, true)]
        [TestCase(PointerEventType.PetHoverEnter, false)]
        [TestCase(PointerEventType.PetHoverLeave, false)]
        public void TriggerPointerEvents(PointerEventType eventType, bool isUserGesture)
        {
            // Arrange
            var input = new PBPointerEvents { IsDirty = true };
            input.PointerEvents.Add(new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetDown,
                EventInfo = new PBPointerEvents.Types.Info(),
            });
            input.PointerEvents.Add(new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetUp,
                EventInfo = new PBPointerEvents.Types.Info(),
            });
            input.PointerEvents.Add(new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetHoverEnter,
                EventInfo = new PBPointerEvents.Types.Info(),
            });
            input.PointerEvents.Add(new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetHoverLeave,
                EventInfo = new PBPointerEvents.Types.Info(),
            });

            world.Add(entity, input);

            // Act
            uiTransformComponent.PointerEventTriggered = eventType;
            system.Update(0);

            // Assert
            Assert.AreEqual(PickingMode.Position, uiTransformComponent.Transform.pickingMode);
            ecsToCRDTWriter.Received(1).AppendMessage(
                Arg.Any<Action<PBPointerEventsResult, (RaycastHit sdkHit, InputAction button, PointerEventType eventType, ISceneStateProvider sceneStateProvider)>>(),
                Arg.Any<CRDTEntity>(),
                Arg.Any<int>(),
                Arg.Any<(RaycastHit sdkHit, InputAction button, PointerEventType eventType, ISceneStateProvider sceneStateProvider)>());
            Assert.IsNull(uiTransformComponent.PointerEventTriggered);
            Assert.AreEqual(1, uiTransformComponent.Drains.DrainCount);
            Assert.AreEqual(1, uiTransformComponent.Drains.AppendCount);
            Assert.AreEqual(eventType, uiTransformComponent.Drains.LastDrained);
            Assert.IsTrue(uiTransformComponent.Drains.LastWasAppended);

            if (isUserGesture)
                sceneStateProvider.Received(1).LastUserInputTick = 123u;
            else
                sceneStateProvider.DidNotReceive().LastUserInputTick = Arg.Any<uint>();
        }

        [Test]
        public void ClearAnUndeclaredEventWithoutAppending()
        {
            // Arrange - the entity declares a down handler only, as a react-ecs onMouseDown does
            var input = new PBPointerEvents { IsDirty = true };
            input.PointerEvents.Add(new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetDown,
                EventInfo = new PBPointerEvents.Types.Info(),
            });
            world.Add(entity, input);

            // Act
            uiTransformComponent.PointerEventTriggered = PointerEventType.PetUp;
            system.Update(0);

            // Assert - the slot is cleared, nothing reaches the scene, and the log says the event was dropped
            ecsToCRDTWriter.DidNotReceive().AppendMessage(
                Arg.Any<Action<PBPointerEventsResult, (RaycastHit sdkHit, InputAction button, PointerEventType eventType, ISceneStateProvider sceneStateProvider)>>(),
                Arg.Any<CRDTEntity>(),
                Arg.Any<int>(),
                Arg.Any<(RaycastHit sdkHit, InputAction button, PointerEventType eventType, ISceneStateProvider sceneStateProvider)>());
            Assert.IsNull(uiTransformComponent.PointerEventTriggered);
            Assert.AreEqual(1, uiTransformComponent.Drains.DrainCount);
            Assert.AreEqual(0, uiTransformComponent.Drains.AppendCount);
            Assert.AreEqual(PointerEventType.PetUp, uiTransformComponent.Drains.LastDrained);
            Assert.IsFalse(uiTransformComponent.Drains.LastWasAppended);
        }

        [Test]
        public void RecordNoDrainWhileNothingIsPending()
        {
            // Arrange
            var input = new PBPointerEvents { IsDirty = true };
            input.PointerEvents.Add(new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetDown,
                EventInfo = new PBPointerEvents.Types.Info(),
            });
            world.Add(entity, input);

            // Act
            system.Update(0);
            system.Update(0);

            // Assert
            Assert.AreEqual(0, uiTransformComponent.Drains.DrainCount);
            Assert.IsNull(uiTransformComponent.Drains.LastDrained);
        }

        [Test]
        public void ResetTheDrainLogWhenTheComponentIsReused()
        {
            // Arrange
            uiTransformComponent.Drains.Record(PointerEventType.PetDown, appended: true);

            // Act
            uiTransformComponent.InitializeAsChild("Reused", new CRDTEntity(501), new CRDTEntity(0));

            // Assert
            Assert.AreEqual(0, uiTransformComponent.Drains.DrainCount);
            Assert.AreEqual(0, uiTransformComponent.Drains.AppendCount);
            Assert.IsNull(uiTransformComponent.Drains.LastDrained);
        }

        [Test]
        public void ResetPickingModeOnPointerEventsRemoval()
        {
            // Arrange - add pointer events
            var pbPointerEvents = new PBPointerEvents { IsDirty = true };
            pbPointerEvents.PointerEvents.Add(new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetDown,
                EventInfo = new PBPointerEvents.Types.Info(),
            });
            world.Add(entity, pbPointerEvents);
            system.Update(0);

            // Act - remove pointer events; PBUiTransform default PointerFilter is not PfmBlock
            world.Remove<PBPointerEvents>(entity);
            system.Update(0);

            // Assert - picking mode should revert to Ignore (default PointerFilter is not PfmBlock)
            Assert.AreEqual(PickingMode.Ignore, uiTransformComponent.Transform.pickingMode);
        }
    }
}
