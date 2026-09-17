using CRDT;
using DCL.ECSComponents;
using DCL.Input;
using DCL.Input.Component;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UIInput;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UIInputReleaseSystemShould : UnitySystemTestBase<UIInputReleaseSystem>
    {
        private IInputBlock inputBlock = null!;
        private Entity entity;
        private UIInputComponent uiInputComponent = null!;

        [SetUp]
        public void SetUp()
        {
            var poolsRegistry = new ComponentPoolsRegistry(
                new Dictionary<Type, IComponentPool>
                {
                    { typeof(UIInputComponent), new ComponentPool.WithDefaultCtor<UIInputComponent>(onRelease: UiElementUtils.ReleaseUIInputComponent) },
                }, new GameObject(nameof(UIInputReleaseSystemShould)).transform);

            inputBlock = Substitute.For<IInputBlock>();
            system = new UIInputReleaseSystem(world, poolsRegistry);

            uiInputComponent = new UIInputComponent();
            uiInputComponent.Initialize(inputBlock, "UIInput", string.Empty, string.Empty, Color.white);

            entity = world.Create(new CRDTEntity(500), new PBUiInput(), uiInputComponent);
        }

        [Test]
        public void RestoreInputMapsWhenFocusedInputEntityIsDestroyed()
        {
            // Arrange
            FocusIn();
            world.Add(entity, new DeleteEntityIntention());

            // Act
            system.Update(0);

            // Assert
            inputBlock.Received(1).Enable(UIInputComponent.BLOCKED_INPUT_KINDS);
            Assert.IsFalse(uiInputComponent.IsFocused);
            Assert.IsFalse(world.Has<UIInputComponent>(entity));
        }

        [Test]
        public void RestoreInputMapsWhenFocusedInputComponentIsRemoved()
        {
            // Arrange
            FocusIn();
            world.Remove<PBUiInput>(entity);

            // Act
            system.Update(0);

            // Assert
            inputBlock.Received(1).Enable(UIInputComponent.BLOCKED_INPUT_KINDS);
            Assert.IsFalse(world.Has<UIInputComponent>(entity));
        }

        [Test]
        public void NotTouchInputMapsWhenUnfocusedInputIsDestroyed()
        {
            // Arrange
            world.Add(entity, new DeleteEntityIntention());

            // Act
            system.Update(0);

            // Assert
            inputBlock.DidNotReceive().Enable(Arg.Any<InputMapComponent.Kind[]>());
        }

        [Test]
        public void NotRestoreInputMapsTwiceWhenInputLostFocusBeforeDestruction()
        {
            // Arrange
            FocusIn();

            using (FocusOutEvent evt = FocusOutEvent.GetPooled())
                uiInputComponent.currentOnFocusOut(evt);

            world.Add(entity, new DeleteEntityIntention());

            // Act
            system.Update(0);

            // Assert
            inputBlock.Received(1).Enable(UIInputComponent.BLOCKED_INPUT_KINDS);
        }

        private void FocusIn()
        {
            using (FocusInEvent evt = FocusInEvent.GetPooled())
                uiInputComponent.currentOnFocusIn(evt);

            inputBlock.Received(1).Disable(UIInputComponent.BLOCKED_INPUT_KINDS);
            Assert.IsTrue(uiInputComponent.IsFocused);
        }
    }
}
