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
    /// <summary>
    ///     Runs against a live runtime panel so the focus events UI Toolkit dispatches (or does not dispatch) are the real ones.
    /// </summary>
    public class UIInputReleaseSystemFocusShould : UnitySystemTestBase<UIInputReleaseSystem>
    {
        private GameObject canvasGameObject = null!;
        private PanelSettings panelSettings = null!;
        private VisualElement root = null!;
        private IInputBlock inputBlock = null!;
        private Entity entity;
        private UIInputComponent uiInputComponent = null!;

        [SetUp]
        public void SetUp()
        {
            canvasGameObject = new GameObject(nameof(UIInputReleaseSystemFocusShould));
            var canvas = canvasGameObject.AddComponent<UIDocument>();
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            canvas.panelSettings = panelSettings;
            root = canvas.rootVisualElement;
            Assert.That(root.panel, Is.Not.Null, "The text field must live in a real panel for focus events to be dispatched.");

            var poolsRegistry = new ComponentPoolsRegistry(
                new Dictionary<Type, IComponentPool>
                {
                    { typeof(UIInputComponent), new ComponentPool.WithDefaultCtor<UIInputComponent>(onRelease: UiElementUtils.ReleaseUIInputComponent) },
                }, canvasGameObject.transform);

            inputBlock = Substitute.For<IInputBlock>();
            system = new UIInputReleaseSystem(world, poolsRegistry);

            uiInputComponent = new UIInputComponent();
            uiInputComponent.Initialize(inputBlock, "UIInput", string.Empty, string.Empty, Color.white);
            root.Add(uiInputComponent.TextField);

            entity = world.Create(new CRDTEntity(500), new PBUiInput(), uiInputComponent);
        }

        protected override void OnTearDown()
        {
            UnityEngine.Object.DestroyImmediate(canvasGameObject);
            UnityEngine.Object.DestroyImmediate(panelSettings);
        }

        [Test]
        public void RestoreInputMapsWhenFocusedInputEntityIsDestroyed()
        {
            // Arrange
            Focus();
            world.Add(entity, new DeleteEntityIntention());

            // Act
            system.Update(0);

            // Assert
            inputBlock.Received(1).Enable(UIInputComponent.BLOCKED_INPUT_KINDS);
            Assert.That(uiInputComponent.IsFocused, Is.False);
            Assert.That(root.panel.focusController.focusedElement, Is.Null);
            Assert.That(uiInputComponent.TextField.panel, Is.Null);
            Assert.That(world.Has<UIInputComponent>(entity), Is.False);
        }

        [Test]
        public void RestoreInputMapsWhenFocusedInputComponentIsRemoved()
        {
            // Arrange
            Focus();
            world.Remove<PBUiInput>(entity);

            // Act
            system.Update(0);

            // Assert
            inputBlock.Received(1).Enable(UIInputComponent.BLOCKED_INPUT_KINDS);
            Assert.That(root.panel.focusController.focusedElement, Is.Null);
            Assert.That(world.Has<UIInputComponent>(entity), Is.False);
        }

        [Test]
        public void RestoreInputMapsWhenFocusedInputWasDetachedBeforeDestruction()
        {
            // Arrange
            Focus();
            uiInputComponent.TextField.RemoveFromHierarchy();
            world.Add(entity, new DeleteEntityIntention());

            // Act
            system.Update(0);

            // Assert
            inputBlock.Received(1).Enable(UIInputComponent.BLOCKED_INPUT_KINDS);
            Assert.That(uiInputComponent.IsFocused, Is.False);
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
            Focus();
            uiInputComponent.TextField.Blur();
            inputBlock.Received(1).Enable(UIInputComponent.BLOCKED_INPUT_KINDS);
            world.Add(entity, new DeleteEntityIntention());

            // Act
            system.Update(0);

            // Assert
            inputBlock.Received(1).Enable(UIInputComponent.BLOCKED_INPUT_KINDS);
        }

        [Test]
        public void BlockSubmitActionWhileFocused()
        {
            // Act
            Focus();

            // Assert
            Assert.That(UIInputComponent.BLOCKED_INPUT_KINDS, Contains.Item(InputMapComponent.Kind.Submit));
        }

        private void Focus()
        {
            uiInputComponent.TextField.Focus();

            inputBlock.Received(1).Disable(UIInputComponent.BLOCKED_INPUT_KINDS);
            Assert.That(uiInputComponent.IsFocused, Is.True);
        }
    }
}
