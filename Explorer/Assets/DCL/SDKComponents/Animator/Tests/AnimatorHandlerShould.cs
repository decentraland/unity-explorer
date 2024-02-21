using Arch.Core;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.Animator.Components;
using DCL.SDKComponents.Animator.Systems;
using DCL.SDKComponents.Tween.Components;
using DCL.SDKComponents.Tween.Helpers;
using DCL.SDKComponents.Tween.Systems;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.Animator.Tests
{
    [TestFixture]
    public class AnimatorHandlerShould : UnitySystemTestBase<AnimatorHandlerSystem>
    {
        private Entity entity;
        private PBAnimator pbAnimator;


        [SetUp]
        public void SetUp()
        {
            system = new AnimatorHandlerSystem(world);

            pbAnimator = new PBAnimator()
            {
                States =
                {
                    new PBAnimationState()
                    {
                        Clip = "GuybrushThreepwood",
                        Loop = true,
                        Playing = true,
                        Speed = 45,
                        Weight = 1,
                        ShouldReset = false
                    }
                }
            };


            entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);
            world.Add(entity, pbAnimator);
        }

        [TearDown]
        public void TearDown()
        {
            system?.Dispose();
        }




        //[Test]
        public void AddAnimatorComponentToEntityWithPBAnimator()
        {
            Assert.AreEqual(0, world.CountEntities(new QueryDescription().WithAll<SDKAnimatorComponent>().WithAll<PBAnimator>()));

            system.Update(0);

            Assert.AreEqual(1, world.CountEntities(new QueryDescription().WithAll<SDKAnimatorComponent>().WithAll<PBAnimator>()));
        }


        //[Test]
        public void DirtyAnimatorComponentIfPBAnimatorIsDirty()
        {
            system.Update(0);
            world.Get<SDKAnimatorComponent>(entity).IsDirty = false;

            pbAnimator.IsDirty = true;
            system.Update(0);

            world.Query(new QueryDescription().WithAll<PBTween>(), (ref SDKAnimatorComponent comp) => Assert.IsTrue(comp.IsDirty));
        }




    }
}
