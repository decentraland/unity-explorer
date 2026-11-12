using CRDT;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UIPointerEvents;
using ECS.TestSuite;
using NUnit.Framework;
using UnityEngine.UIElements;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UIPointerEventsSystemShould : UnitySystemTestBase<UIPointerEventsSystem>
    {
        private Entity entity;
        private UITransformComponent uiTransformComponent = null!;

        [SetUp]
        public void SetUp()
        {
            system = new UIPointerEventsSystem(world);
            entity = world.Create();
            uiTransformComponent = AddUITransformToEntity(entity);
            world.Add(entity, new CRDTEntity(500));
        }

        [Test]
        public void RegisterCallbacksOnceWhenPointerEventsAreDirty()
        {
            // Arrange
            world.Add(entity, CreatePointerEvents(PointerEventType.PetDown));

            // Act
            system.Update(0);
            EventCallback<PointerDownEvent>? registered = uiTransformComponent.currentOnPointerDownCallback;
            system.Update(0);

            // Assert
            Assert.IsNotNull(registered);
            Assert.AreSame(registered, uiTransformComponent.currentOnPointerDownCallback);
            Assert.IsFalse(world.Get<PBPointerEvents>(entity).IsDirty);
        }

        [Test]
        public void QueueEveryDispatchedPointerEventInOrder()
        {
            // Arrange
            world.Add(entity, CreatePointerEvents(PointerEventType.PetDown));
            system.Update(0);

            // Act
            uiTransformComponent.currentOnPointerDownCallback!(null!);
            uiTransformComponent.currentOnPointerUpCallback!(null!);
            uiTransformComponent.currentOnPointerEnterCallback!(null!);

            // Assert
            CollectionAssert.AreEqual(
                new[] { PointerEventType.PetDown, PointerEventType.PetUp, PointerEventType.PetHoverEnter },
                uiTransformComponent.PendingPointerEvents);
        }

        [Test]
        public void ResetPickingModeAndDropQueuedEventsOnPointerEventsRemoval()
        {
            // Arrange - add pointer events; PBUiTransform default PointerFilter is not PfmBlock
            world.Add(entity, CreatePointerEvents(PointerEventType.PetDown));
            uiTransformComponent.Transform.pickingMode = PickingMode.Position;
            system.Update(0);
            uiTransformComponent.PendingPointerEvents.Add(PointerEventType.PetDown);

            // Act
            world.Remove<PBPointerEvents>(entity);
            system.Update(0);

            // Assert
            Assert.AreEqual(PickingMode.Ignore, uiTransformComponent.Transform.pickingMode);
            Assert.IsEmpty(uiTransformComponent.PendingPointerEvents);
        }

        [Test]
        public void ResetTheDrainLogWhenTheComponentIsReused()
        {
            // Arrange
            uiTransformComponent.Drains.RecordAppended(PointerEventType.PetDown);

            // Act
            uiTransformComponent.InitializeAsChild("Reused", new CRDTEntity(501), new CRDTEntity(0));

            // Assert
            Assert.AreEqual(0, uiTransformComponent.Drains.AppendedCount(PointerEventType.PetDown));
        }

        private static PBPointerEvents CreatePointerEvents(PointerEventType eventType)
        {
            var pbPointerEvents = new PBPointerEvents { IsDirty = true };

            pbPointerEvents.PointerEvents.Add(new PBPointerEvents.Types.Entry
            {
                EventType = eventType,
                EventInfo = new PBPointerEvents.Types.Info(),
            });

            return pbPointerEvents;
        }
    }
}
