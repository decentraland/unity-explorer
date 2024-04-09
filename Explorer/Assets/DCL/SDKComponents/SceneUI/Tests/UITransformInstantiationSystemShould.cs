using Arch.Core;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UITransform;
using DCL.SDKComponents.SceneUI.Utils;
using NUnit.Framework;
using System.Threading.Tasks;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UITransformInstantiationSystemShould : UITransformSystemTestBase<UITransformInstantiationSystem>
    {

        public async Task SetUp()
        {
            await Initialize();
            system = new UITransformInstantiationSystem(world, canvas, poolsRegistry);
        }


        public void InstantiateUITransform()
        {
            // Act
            base.CreateUITransform();

            // Assert
            UITransformComponent uiTransformComponent = world.Get<UITransformComponent>(entity);
            Assert.IsNotNull(uiTransformComponent);
            Assert.AreEqual(UiElementUtils.BuildElementName("UITransform", entity), uiTransformComponent.Transform.name);
            Assert.IsTrue(canvas.rootVisualElement.Contains(uiTransformComponent.Transform));
            Assert.AreEqual(EntityReference.Null, uiTransformComponent.Parent);
            Assert.AreEqual(0, uiTransformComponent.Children.Count);
            Assert.IsFalse(uiTransformComponent.IsHidden);
        }
    }
}
