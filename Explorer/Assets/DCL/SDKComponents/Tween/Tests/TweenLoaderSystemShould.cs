using Arch.Core;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.Tween.Components;
using DCL.SDKComponents.Tween.Helpers;
using DCL.SDKComponents.Tween.Systems;
using DCL.Utilities;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.Tween.Tests
{
    [TestFixture]
    public class TweenLoaderSystemShould : UnitySystemTestBase<TweenLoaderSystem>
    {
        private Entity entity;
        private PBTween pbTween;


        [SetUp]
        public void SetUp()
        {
            var poolsRegistry = new ComponentPoolsRegistry(
                new Dictionary<Type, IComponentPool> { }, null);

            var sdkPool = poolsRegistry.AddComponentPool<SDKTweenComponent>();
            system = new TweenLoaderSystem(world, sdkPool);

            var startVector = new Decentraland.Common.Vector3() { X = 0, Y = 0, Z = 0};
            var endVector = new Decentraland.Common.Vector3() { X = 10, Y = 0, Z = 0 };
            var move = new Move() { End = endVector, Start = startVector };
            pbTween = new PBTween()
            {
                CurrentTime = 0,
                Duration = 1000,
                EasingFunction = EasingFunction.EfLinear,
                IsDirty = true,
                Move = move,
                Playing = true,
            };

            entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);
            world.Add(entity, pbTween);
        }

        [TearDown]
        public void TearDown()
        {
            system?.Dispose();
        }


        [Test]
        public void AddTweenComponentWithCorrectModelToEntityWithPBTween()
        {
            Assert.AreEqual(0, world.CountEntities(new QueryDescription().WithAll<TweenComponent>()));

            system.Update(0);

            Assert.AreEqual(1, world.CountEntities(new QueryDescription().WithAll<TweenComponent>()));

            world.Query(new QueryDescription().WithAll<PBTween>(), (ref TweenComponent comp) => Assert.IsTrue(TweenSDKComponentHelper.AreSameModels(pbTween, comp.SDKTweenComponent.CurrentTweenModel)));
        }

        [Test]
        public void UpdateTweenComponentIfPBTweenIsDifferentThanStoredModel()
        {
            system.Update(0);
            pbTween.CurrentTime = 5555;

            world.Query(new QueryDescription().WithAll<PBTween>(), (ref TweenComponent comp) => Assert.IsFalse(TweenSDKComponentHelper.AreSameModels(pbTween, comp.SDKTweenComponent.CurrentTweenModel)));

            system.Update(0);

            world.Query(new QueryDescription().WithAll<PBTween>(), (ref TweenComponent comp) => Assert.IsTrue(TweenSDKComponentHelper.AreSameModels(pbTween, comp.SDKTweenComponent.CurrentTweenModel)));
            world.Query(new QueryDescription().WithAll<PBTween>(), (ref TweenComponent comp) => Assert.IsTrue(comp.SDKTweenComponent.IsDirty));
        }

        [Test]
        public void DirtyTweenComponentIfPBTweenIsDirty()
        {
            system.Update(0);
            world.Get<TweenComponent>(entity).SDKTweenComponent.IsDirty = false;

            pbTween.IsDirty = true;
            system.Update(0);

            world.Query(new QueryDescription().WithAll<PBTween>(), (ref TweenComponent comp) => Assert.IsTrue(comp.SDKTweenComponent.IsDirty));
        }

        [Test]
        public void DontUpdateTweenComponentIfPBTweenIsNotDifferentThanStoredModelAndNotDirty()
        {
            system.Update(0);
            world.Get<TweenComponent>(entity).SDKTweenComponent.IsDirty = false;

            pbTween.IsDirty = false;
            system.Update(0);

            world.Query(new QueryDescription().WithAll<PBTween>(), (ref TweenComponent comp) => Assert.IsFalse(comp.SDKTweenComponent.IsDirty));
        }
    }
}
