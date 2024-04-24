using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Classes;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Defaults;
using DCL.SDKComponents.SceneUI.Systems.UIBackground;
using Decentraland.Common;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using System;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UIBackgroundInstantiationSystemShould : UnitySystemTestBase<UIBackgroundInstantiationSystem>
    {
        private IComponentPoolsRegistry poolsRegistry;
        private ISceneData sceneData;
        private IPerformanceBudget frameTimeBudgetProvider;
        private IPerformanceBudget memoryBudgetProvider;
        private Entity entity;
        private UITransformComponent uiTransformComponent;

        [SetUp]
        public void SetUp()
        {
            poolsRegistry = new ComponentPoolsRegistry(
                new Dictionary<Type, IComponentPool>
                {
                    { typeof(DCLImage), new ComponentPool.WithDefaultCtor<DCLImage>() },
                }, null);

            sceneData = Substitute.For<ISceneData>();
            frameTimeBudgetProvider = Substitute.For<IPerformanceBudget>();
            memoryBudgetProvider = Substitute.For<IPerformanceBudget>();

            system = new UIBackgroundInstantiationSystem(world, poolsRegistry, sceneData, frameTimeBudgetProvider, memoryBudgetProvider);
            entity = world.Create();
            uiTransformComponent = AddUITransformToEntity(entity);
            world.Add(entity, PartitionComponent.TOP_PRIORITY);
        }

        [Test]
        public void InstantiateUIBackground()
        {
            // Arrange
            var input = new PBUiBackground();

            // Act
            world.Add(entity, input);
            system.Update(0);

            // Assert
            ref UIBackgroundComponent uiBackgroundComponent = ref world.Get<UIBackgroundComponent>(entity);
            Assert.IsNotNull(uiBackgroundComponent.Image);
        }

        [Test]
        public void UpdateUIBackground()
        {
            // Arrange
            var input = new PBUiBackground();
            world.Add(entity, input);
            system.Update(0);
            const int NUMBER_OF_UPDATES = 3;

            frameTimeBudgetProvider.TrySpendBudget().Returns(true);
            memoryBudgetProvider.TrySpendBudget().Returns(true);

            for (var i = 0; i < NUMBER_OF_UPDATES; i++)
            {
                // Act
                input.Color = new Color4 { R = i, G = 1, B = 1, A = 1 };
                input.IsDirty = true;
                system.Update(0);

                // Assert
                Assert.IsTrue(input.GetColor() == uiTransformComponent.Transform.style.backgroundColor);
            }
        }
    }
}
