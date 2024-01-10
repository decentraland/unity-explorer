using Cysharp.Threading.Tasks;
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
        [SetUp]
        public async void SetUp()
        {
            await base.Initialize();
            system = new UITransformUpdateSystem(world, canvas, sceneStateProvider);
        }

        [Test]
        public async Task UpdateUITransform()
        {
            // For some reason SetUp is not awaited, probably a Unity's bug
            await UniTask.WaitUntil(() => system != null);

            // Arrange
            var input = base.CreateUITransform();
            const int NUMBER_OF_UPDATES = 3;
            sceneStateProvider.IsCurrent = true;

            for (var i = 0; i < NUMBER_OF_UPDATES; i++)
            {
                // Act
                input.Display = (YGDisplay) i;
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
        public async Task CheckUITransformOutOfScene(bool isCurrentScene)
        {
            // For some reason SetUp is not awaited, probably a Unity's bug
            await UniTask.WaitUntil(() => system != null);

            // Arrange
            var input = base.CreateUITransform();
            sceneStateProvider.IsCurrent = isCurrentScene;
            var uiTransformComponent = world.Get<UITransformComponent>(entity);
            uiTransformComponent.IsHidden = isCurrentScene;

            // Act
            system.Update(0);

            // Assert
            Assert.AreEqual(isCurrentScene, canvas.rootVisualElement.Contains(uiTransformComponent.Transform));
        }
    }
}
