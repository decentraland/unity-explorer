using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UIPointerEvents;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using UnityEngine;
using UnityEngine.UIElements;
using Entity = Arch.Core.Entity;
using RaycastHit = DCL.ECSComponents.RaycastHit;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UIPointerEventsSystemShould : UnitySystemTestBase<UIPointerEventsSystem>
    {
        private Entity entity;
        private UITransformComponent uiTransformComponent;
        private ISceneStateProvider sceneStateProvider;
        private IECSToCRDTWriter ecsToCRDTWriter;

        [SetUp]
        public void SetUp()
        {
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            system = new UIPointerEventsSystem(world, sceneStateProvider, ecsToCRDTWriter);
            entity = world.Create();
            uiTransformComponent = AddUITransformToEntity(entity);
            world.Add(entity, new CRDTEntity(500));
        }

        [Test]
        [TestCase(PointerEventType.PetDown)]
        [TestCase(PointerEventType.PetUp)]
        [TestCase(PointerEventType.PetHoverEnter)]
        [TestCase(PointerEventType.PetHoverLeave)]
        public void TriggerPointerEvents(PointerEventType eventType)
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
        }

        [Test]
        public void SetupUiButtonOnTextEntityWithPointerEvents()
        {
            // Arrange - entity with PBUiText + PBPointerEvents (no PBUiDropdown, PBUiInput, UiButtonComponent)
            var pbPointerEvents = new PBPointerEvents { IsDirty = true };
            pbPointerEvents.PointerEvents.Add(new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetDown,
                EventInfo = new PBPointerEvents.Types.Info(),
            });
            world.Add(entity, new PBUiText(), pbPointerEvents);

            // Act
            system.Update(0);

            // Assert - UiButtonComponent should be added
            Assert.IsTrue(world.Has<UiButtonComponent>(entity));
        }

        [Test]
        public void ApplyDefaultTransformValuesOnSetupUiButton()
        {
            // Arrange
            var pbPointerEvents = new PBPointerEvents { IsDirty = true };
            pbPointerEvents.PointerEvents.Add(new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetDown,
                EventInfo = new PBPointerEvents.Types.Info(),
            });
            world.Add(entity, new PBUiText(), pbPointerEvents);

            // Act
            system.Update(0);

            // Assert - default transform values should be applied
            var transformStyle = uiTransformComponent.Transform.style;
            Assert.AreEqual(Overflow.Hidden, transformStyle.overflow.value);
            Assert.AreEqual(new StyleLength(10f), transformStyle.borderBottomLeftRadius);
            Assert.AreEqual(new StyleLength(10f), transformStyle.borderTopLeftRadius);
            Assert.AreEqual(new StyleFloat(1f), transformStyle.borderTopWidth);
            Assert.AreEqual(new StyleColor(Color.gray), transformStyle.borderTopColor);
        }

        [Test]
        public void ApplyDefaultBackgroundOnSetupUiButton()
        {
            // Arrange - entity without PBUiBackground
            var pbPointerEvents = new PBPointerEvents { IsDirty = true };
            pbPointerEvents.PointerEvents.Add(new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetDown,
                EventInfo = new PBPointerEvents.Types.Info(),
            });
            world.Add(entity, new PBUiText(), pbPointerEvents);

            // Act
            system.Update(0);

            // Assert - white background should be applied
            Assert.AreEqual(new StyleColor(Color.white), uiTransformComponent.Transform.style.backgroundColor);
        }

        [Test]
        public void NotSetupUiButtonOnEntityWithPBUiDropdown()
        {
            // Arrange - entity has PBUiDropdown, so SetupUiButton should be skipped
            var pbPointerEvents = new PBPointerEvents { IsDirty = true };
            pbPointerEvents.PointerEvents.Add(new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetDown,
                EventInfo = new PBPointerEvents.Types.Info(),
            });
            world.Add(entity, new PBUiText(), pbPointerEvents, new PBUiDropdown());

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(world.Has<UiButtonComponent>(entity));
        }

        [Test]
        public void NotSetupUiButtonOnEntityWithPBUiInput()
        {
            // Arrange - entity has PBUiInput, so SetupUiButton should be skipped
            var pbPointerEvents = new PBPointerEvents { IsDirty = true };
            pbPointerEvents.PointerEvents.Add(new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetDown,
                EventInfo = new PBPointerEvents.Types.Info(),
            });
            world.Add(entity, new PBUiText(), pbPointerEvents, new PBUiInput());

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(world.Has<UiButtonComponent>(entity));
        }

        [Test]
        public void NotSetupUiButtonWhenAlreadyHasUiButtonComponent()
        {
            // Arrange - entity already has UiButtonComponent (should be [None] filtered)
            var pbPointerEvents = new PBPointerEvents { IsDirty = true };
            pbPointerEvents.PointerEvents.Add(new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetDown,
                EventInfo = new PBPointerEvents.Types.Info(),
            });
            world.Add(entity, new PBUiText(), pbPointerEvents, new UiButtonComponent());

            // Act & Assert - should not throw or add a second UiButtonComponent
            Assert.DoesNotThrow(() => system.Update(0));
            Assert.IsTrue(world.Has<UiButtonComponent>(entity));
        }

        [Test]
        public void SetupUiButtonWithShowFeedbackDisabled()
        {
            // Arrange - pointer events with ShowFeedback=false
            var pbPointerEvents = new PBPointerEvents { IsDirty = true };
            pbPointerEvents.PointerEvents.Add(new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetHoverEnter,
                EventInfo = new PBPointerEvents.Types.Info { ShowFeedback = false },
            });
            world.Add(entity, new PBUiText(), pbPointerEvents);

            // Act
            system.Update(0);

            // Assert - UiButtonComponent should still be added even with feedback disabled
            Assert.IsTrue(world.Has<UiButtonComponent>(entity));
        }

        [Test]
        public void UpdateUIButtonTransformDefaultsWhenDirty()
        {
            // Arrange - setup the button first
            var pbPointerEvents = new PBPointerEvents { IsDirty = true };
            pbPointerEvents.PointerEvents.Add(new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetDown,
                EventInfo = new PBPointerEvents.Types.Info(),
            });
            world.Add(entity, new PBUiText(), pbPointerEvents);
            system.Update(0);

            // Act - mark PBUiTransform as dirty
            ref PBUiTransform pbUiTransform = ref world.Get<PBUiTransform>(entity);
            pbUiTransform.IsDirty = true;
            system.Update(0);

            // Assert - defaults should be reapplied
            Assert.AreEqual(Overflow.Hidden, uiTransformComponent.Transform.style.overflow.value);
        }

        [Test]
        public void RemoveUiButtonComponentOnPointerEventsRemoval()
        {
            // Arrange - setup the button and pointer events
            var pbPointerEvents = new PBPointerEvents { IsDirty = true };
            pbPointerEvents.PointerEvents.Add(new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetDown,
                EventInfo = new PBPointerEvents.Types.Info(),
            });
            world.Add(entity, new PBUiText(), pbPointerEvents);
            system.Update(0);

            Assert.IsTrue(world.Has<UiButtonComponent>(entity));

            // Act - remove PBPointerEvents
            world.Remove<PBPointerEvents>(entity);
            system.Update(0);

            // Assert
            Assert.IsFalse(world.Has<UiButtonComponent>(entity));
        }

        [Test]
        public void RemoveUiButtonComponentOnEntityDestruction()
        {
            // Arrange - setup the button and pointer events
            var pbPointerEvents = new PBPointerEvents { IsDirty = true };
            pbPointerEvents.PointerEvents.Add(new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetDown,
                EventInfo = new PBPointerEvents.Types.Info(),
            });
            world.Add(entity, new PBUiText(), pbPointerEvents);
            system.Update(0);

            Assert.IsTrue(world.Has<UiButtonComponent>(entity));

            // Act - add DeleteEntityIntention
            world.Add(entity, new DeleteEntityIntention());
            system.Update(0);

            // Assert
            Assert.IsFalse(world.Has<UiButtonComponent>(entity));
        }

        [Test]
        public void ResetPickingModeOnPointerEventsRemoval()
        {
            // Arrange - add pointer events with PfmBlock filter
            var pbPointerEvents = new PBPointerEvents { IsDirty = true };
            pbPointerEvents.PointerEvents.Add(new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetDown,
                EventInfo = new PBPointerEvents.Types.Info(),
            });
            world.Add(entity, new PBUiText(), pbPointerEvents);
            system.Update(0);

            // Act - remove pointer events; PBUiTransform default PointerFilter is not PfmBlock
            world.Remove<PBPointerEvents>(entity);
            system.Update(0);

            // Assert - picking mode should revert to Ignore (default PointerFilter is not PfmBlock)
            Assert.AreEqual(PickingMode.Ignore, uiTransformComponent.Transform.pickingMode);
        }
    }
}
