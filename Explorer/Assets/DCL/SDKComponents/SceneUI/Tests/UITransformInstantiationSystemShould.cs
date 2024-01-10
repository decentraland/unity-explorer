using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UITransform;
using ECS.TestSuite;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UITransformInstantiationSystemShould : UnitySystemTestBase<UITransformInstantiationSystem>
    {
        private const string SCENES_UI_ROOT_CANVAS = "ScenesUIRootCanvas";

        private IComponentPoolsRegistry poolsRegistry;
        private Entity entity;
        private UIDocument canvas;

        [SetUp]
        public async void SetUp()
        {
            canvas = Object.Instantiate(await Addressables.LoadAssetAsync<GameObject>(SCENES_UI_ROOT_CANVAS)).GetComponent<UIDocument>();

            poolsRegistry = new ComponentPoolsRegistry(
                new Dictionary<Type, IComponentPool>
                {
                    { typeof(VisualElement), new ComponentPool<VisualElement>() },
                }, null);

            system = new UITransformInstantiationSystem(world, canvas, poolsRegistry);
            entity = world.Create();
        }

        [Test]
        public async Task InstantiateUITransform()
        {
            // For some reason SetUp is not awaited, probably a Unity's bug
            await UniTask.WaitUntil(() => system != null && entity != null);

            //Arrange
            var input = new PBUiTransform();

            //Act
            world.Add(entity, input);
            system.Update(0);

            //Assert
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
