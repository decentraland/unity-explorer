﻿using Arch.Core;
using CRDT;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UITransform;
using DCL.SDKComponents.SceneUI.Utils;
using NUnit.Framework;
using System.Threading.Tasks;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UITransformInstantiationSystemShould : UITransformSystemTestBase<UITransformInstantiationSystem>
    {
        [SetUp]
        public async Task SetUp()
        {
            await Initialize();
            CreateUITransform();
        }

        [Test]
        public void InstantiateUITransform()
        {
            // Assert
            UITransformComponent uiTransformComponent = world.Get<UITransformComponent>(entity);
            Assert.IsNotNull(uiTransformComponent);
            Assert.AreEqual(UiElementUtils.BuildElementName("UITransform", world.Get<CRDTEntity>(entity)), uiTransformComponent.Transform.name);
            Assert.IsTrue(canvas.rootVisualElement.Contains(uiTransformComponent.Transform));
            Assert.AreEqual(Entity.Null, uiTransformComponent.RelationData.parent);
            Assert.AreEqual(new CRDTEntity(0), uiTransformComponent.RelationData.rightOf);
            Assert.AreEqual(null, uiTransformComponent.RelationData.head);
            Assert.IsFalse(uiTransformComponent.IsHidden);
        }
    }
}
