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

        [Test]
        public void LoadTweenSequenceWithMoveRotateScaleStepWithOmittedFields()
        {
            // Sequence where first tween is MoveRotateScale with only position set (rotation and scale omitted).
            // Loader should still add SDKTweenSequenceComponent; resolution of omitted fields happens at runtime in updater.
            var moveRotateScale = new MoveRotateScale
            {
                PositionStart = new Vector3 { X = 0, Y = 0, Z = 0 },
                PositionEnd = new Vector3 { X = 5, Y = 0, Z = 0 }
                // RotationStart, RotationEnd, ScaleStart, ScaleEnd omitted
            };
            var firstTween = new PBTween
            {
                CurrentTime = 0,
                Duration = 1000,
                EasingFunction = EasingFunction.EfLinear,
                IsDirty = true,
                Playing = true,
                MoveRotateScale = moveRotateScale
            };
            var pbTweenSequence = new PBTweenSequence { IsDirty = true };

            var entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);
            world.Add(entity, firstTween, pbTweenSequence);

            Assert.AreEqual(0, world.CountEntities(new QueryDescription().WithAll<SDKTweenSequenceComponent>()));
            // SetUp already added one entity with PBTween+PBTweenSequence; we added another with MoveRotateScale (omitted fields)
            system.Update(0);

            Assert.AreEqual(2, world.CountEntities(new QueryDescription().WithAll<SDKTweenSequenceComponent>()));
            Assert.IsTrue(world.Has<SDKTweenSequenceComponent>(entity),
                "Loader should add SDKTweenSequenceComponent for MoveRotateScale step with omitted rotation/scale");
        }
    }
}

