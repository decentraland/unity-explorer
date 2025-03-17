using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Defaults;
using DCL.SDKComponents.SceneUI.Systems.UIDropdown;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UIDropdownInstantiationSystemShould : UnitySystemTestBase<UIDropdownInstantiationSystem>
    {
        private IComponentPoolsRegistry poolsRegistry;
        private IECSToCRDTWriter ecsToCRDTWriter;
        private Entity entity;
        private UITransformComponent uiTransformComponent;
        private PBUiDropdown input;

        [SetUp]
        public void SetUp()
        {
            poolsRegistry = new ComponentPoolsRegistry(
                new Dictionary<Type, IComponentPool>
                {
                    { typeof(UIDropdownComponent), new ComponentPool.WithDefaultCtor<UIDropdownComponent>() },
                }, null);

            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();
            system = new UIDropdownInstantiationSystem(world, poolsRegistry, ecsToCRDTWriter);
            entity = world.Create();
            uiTransformComponent = AddUITransformToEntity(entity);
            world.Add(entity, new CRDTEntity(500));

            input = new PBUiDropdown();
            input.Options.Add("TestOption1");
            input.Options.Add("TestOption2");
            input.Options.Add("TestOption3");
            world.Add(entity, input);
            system.Update(0);
        }

        [Test]
        public void InstantiateUIDropdown()
        {
            // Assert
            ref UIDropdownComponent uiDropdownComponent = ref world.Get<UIDropdownComponent>(entity);
            Assert.AreEqual(UiElementUtils.BuildElementName("UIDropdown", entity), uiDropdownComponent.DropdownField.name);
            Assert.IsTrue(uiDropdownComponent.DropdownField.ClassListContains("dcl-dropdown"));
            Assert.AreEqual(PickingMode.Position, uiDropdownComponent.DropdownField.pickingMode);
            Assert.IsTrue(uiTransformComponent.Transform.Contains(uiDropdownComponent.DropdownField));
            Assert.IsNotNull(uiDropdownComponent.DropdownField);
            Assert.IsNotNull(uiDropdownComponent.TextElement);
            Assert.IsTrue(uiDropdownComponent.TextElement.ClassListContains("unity-base-popup-field__text"));
        }

        [Test]
        public void UpdateUIDropdown()
        {
            // Arrange
            const int NUMBER_OF_UPDATES = 3;

            for (var i = 0; i < NUMBER_OF_UPDATES; i++)
            {
                // Act
                for (var j = 0; j < i+1; j++) input.Options.Add((j+1).ToString());
                input.FontSize = i + 1;
                input.TextAlign = (TextAlignMode) i;
                input.IsDirty = true;
                system.Update(0);

                // Assert
                ref UIDropdownComponent uiDropdownComponent = ref world.Get<UIDropdownComponent>(entity);
                Assert.AreEqual(input.Options.Count, uiDropdownComponent.DropdownField.choices.Count);
                Assert.IsTrue(input.GetFontSize() == uiDropdownComponent.DropdownField.style.fontSize);
                Assert.IsTrue(input.GetTextAlign() == uiDropdownComponent.TextElement.style.unityTextAlign);
            }
        }

        [Test]
        public void TriggerDropdownResults()
        {
            // Arrange
            input.IsDirty = true;
            system.Update(0);
            const int TEST_INDEX = 1;
            ref UIDropdownComponent uiDropdownComponent = ref world.Get<UIDropdownComponent>(entity);
            uiDropdownComponent.DropdownField.index = TEST_INDEX;
            uiDropdownComponent.IsOnValueChangedTriggered = true;
            system.Update(0);

            // Act
            system.Update(0);

            // Assert
            ecsToCRDTWriter.Received(1).PutMessage(Arg.Any<Action<PBUiDropdownResult, int>>(), Arg.Any<CRDTEntity>(), TEST_INDEX);
            Assert.IsFalse(uiDropdownComponent.IsOnValueChangedTriggered);
        }
    }
}
