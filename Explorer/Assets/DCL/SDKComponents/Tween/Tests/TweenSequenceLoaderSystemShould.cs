using Arch.Core;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using DCL.SDKComponents.Tween.Systems;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NUnit.Framework;
using Decentraland.Common;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.Tween.Tests
{
    [TestFixture]
    public class TweenSequenceLoaderSystemShould : UnitySystemTestBase<TweenSequenceLoaderSystem>
    {
        [SetUp]
        public void SetUp()
        {
            system = new TweenSequenceLoaderSystem(world);

            var startVector = new Vector3
                { X = 0, Y = 0, Z = 0 };

            var endVector = new Vector3
                { X = 10, Y = 0, Z = 0 };

            var move = new Move
                { End = endVector, Start = startVector };

            pbTween = new PBTween
            {
                CurrentTime = 0,
                Duration = 1000,
                EasingFunction = EasingFunction.EfLinear,
                IsDirty = true,
                Move = move,
                Playing = true,
            };

            pbTweenSequence = new PBTweenSequence
            {
                IsDirty = true,
            };

            entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);
            world.Add(entity, pbTween, pbTweenSequence);
        }

        [TearDown]
        public void TearDown()
        {
            system?.Dispose();
        }

        private Entity entity;
        private PBTween pbTween;
        private PBTweenSequence pbTweenSequence;

        [Test]
        public void AddTweenSequenceComponentWithCorrectModelToEntityWithPBTweenAndPBTweenSequence()
        {
            Assert.AreEqual(0, world.CountEntities(new QueryDescription().WithAll<SDKTweenSequenceComponent>()));

            system.Update(0);

            Assert.AreEqual(1, world.CountEntities(new QueryDescription().WithAll<SDKTweenSequenceComponent>()));
        }
    }
}

