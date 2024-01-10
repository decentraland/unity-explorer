using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UITransform;
using NUnit.Framework;
using System.Threading.Tasks;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UITransformInstantiationSystemShould : UITransformSystemTestBase<UITransformInstantiationSystem>
    {
        [SetUp]
        public async void SetUp()
        {
            await base.Initialize();
            system = new UITransformInstantiationSystem(world, canvas, poolsRegistry);
        }

        [Test]
        public async Task InstantiateUITransform()
        {
            // For some reason SetUp is not awaited, probably a Unity's bug
            await UniTask.WaitUntil(() => system != null);

            // Act
            base.CreateUITransform();

            // Assert
            UITransformComponent uiTransformComponent = world.Get<UITransformComponent>(entity);
            Assert.IsNotNull(uiTransformComponent.Transform);
            Assert.AreEqual($"UITransform (Entity {entity.Id})", uiTransformComponent.Transform.name);
            Assert.IsTrue(canvas.rootVisualElement.Contains(uiTransformComponent.Transform));
            Assert.AreEqual(EntityReference.Null, uiTransformComponent.Parent);
            Assert.AreEqual(0, uiTransformComponent.Children.Count);
            Assert.IsFalse(uiTransformComponent.IsHidden);
        }
    }
}
