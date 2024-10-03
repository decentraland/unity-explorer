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
using Decentraland.Common;
using UnityEngine.Pool;
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
            system = new TweenLoaderSystem(world);

            var startVector = new Vector3() { X = 0, Y = 0, Z = 0};
            var endVector = new Vector3() { X = 10, Y = 0, Z = 0 };
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

        [Test]
        public void AddTweenComponentWithCorrectModelToEntityWithPBTween()
        {
            Assert.AreEqual(0, world.CountEntities(new QueryDescription().WithAll<SDKTweenComponent>()));

            system.Update(0);

            Assert.AreEqual(1, world.CountEntities(new QueryDescription().WithAll<SDKTweenComponent>()));
        }
    }
}
