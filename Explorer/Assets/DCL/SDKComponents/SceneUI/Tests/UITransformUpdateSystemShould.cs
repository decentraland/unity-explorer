using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UITransform;
using DCL.SDKComponents.SceneUI.Utils;
using NUnit.Framework;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UITransformUpdateSystemShould : UITransformSystemTestBase<UITransformUpdateSystem>
    {

        public async void SetUp()
        {
            await Initialize();
            system = new UITransformUpdateSystem(world, canvas, sceneStateProvider);
        }


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




        public void CheckUITransformOutOfScene(bool isCurrentScene)
        {
            // Arrange
            PBUiTransform input = CreateUITransform();
            sceneStateProvider.IsCurrent = isCurrentScene;
            UITransformComponent uiTransformComponent = world.Get<UITransformComponent>(entity);
            uiTransformComponent.IsHidden = isCurrentScene;

            // Act
            system.Update(0);

            // Assert
            Assert.AreEqual(isCurrentScene, canvas.rootVisualElement.Contains(uiTransformComponent.Transform));
        }
    }
}
