﻿using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UITransform;
using DCL.SDKComponents.SceneUI.Utils;
using NUnit.Framework;
using System.Threading.Tasks;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UITransformUpdateSystemShould : UITransformSystemTestBase<UITransformUpdateSystem>
    {
        private Entity sceneRoot;

        [SetUp]
        public async Task SetUp()
        {
            await Initialize();
            system = new UITransformUpdateSystem(world, canvas, sceneStateProvider, sceneRoot = world.Create(new CRDTEntity(SpecialEntitiesID.SCENE_ROOT_ENTITY)));
        }

        [Test]
        public void UpdateUITransform()
        {
            // Arrange
            PBUiTransform input = CreateUITransform();
            const int NUMBER_OF_UPDATES = 3;
            sceneStateProvider.IsCurrent = true;

            for (var i = 0; i < NUMBER_OF_UPDATES; i++)
            {
                // Act
                input.Display = (YGDisplay)i;

                // Space to set the properties that we want to test
                // ...
                input.IsDirty = true;
                system.Update(0);

                // Assert
                UITransformComponent uiTransformComponent = world.Get<UITransformComponent>(entity);
                Assert.AreEqual(UiElementUtils.GetDisplay(input.Display), uiTransformComponent.Transform.style.display);
            }
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void CheckUITransformOutOfScene(bool isCurrentScene)
        {
            // Arrange
            PBUiTransform input = CreateUITransform();
            sceneStateProvider.IsCurrent = isCurrentScene;
            UITransformComponent uiTransformComponent = world.Get<UITransformComponent>(entity);
            uiTransformComponent.IsHidden = isCurrentScene;
            uiTransformComponent.RelationData.parent = sceneRoot;

            // Act
            system.Update(0);

            // Assert
            Assert.AreEqual(isCurrentScene, canvas.rootVisualElement.Contains(uiTransformComponent.Transform));
        }
    }
}
