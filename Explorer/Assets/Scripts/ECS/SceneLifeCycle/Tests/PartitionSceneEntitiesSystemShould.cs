using Arch.Core;
using DCL.PerformanceAndDiagnostics.Optimization.Pools;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.TestSuite;
using Ipfs;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Tests
{
    public class PartitionSceneEntitiesSystemShould : UnitySystemTestBase<PartitionSceneEntitiesSystem>
    {
        private IPartitionSettings partitionSettings;
        private IReadOnlyCameraSamplingData samplingData;
        private IComponentPool<PartitionComponent> componentPool;

        [SetUp]
        public void SetUp()
        {
            partitionSettings = Substitute.For<IPartitionSettings>();
            partitionSettings.AngleTolerance.Returns(0);
            partitionSettings.PositionSqrTolerance.Returns(0);
            partitionSettings.FastPathSqrDistance.Returns(int.MaxValue);

            samplingData = Substitute.For<IReadOnlyCameraSamplingData>();
            componentPool = Substitute.For<IComponentPool<PartitionComponent>>();
            componentPool.Get().Returns(_ => new PartitionComponent());

            system = new PartitionSceneEntitiesSystem(world, componentPool, partitionSettings, samplingData);
        }

        [Test]
        public void PartitionNewEntity([Values(true, false)] bool isDirty)
        {
            samplingData.IsDirty.Returns(isDirty);
            samplingData.Forward.Returns(Vector3.forward);
            samplingData.Position.Returns(new Vector3(0, 0, 46)); // Partition #1
            samplingData.Parcel.Returns(ParcelMathHelper.FloorToParcel(new Vector3(0, 0, 46)));

            partitionSettings.SqrDistanceBuckets.Returns(new[] { 16 * 16, 32 * 32, 64 * 64 });

            Entity e = world.Create(new SceneDefinitionComponent(new IpfsTypes.SceneEntityDefinition(), new[] { ParcelMathHelper.FloorToParcel(Vector3.zero) }, new IpfsTypes.IpfsPath()));

            system.Update(0);

            Assert.That(world.TryGet(e, out PartitionComponent partitionComponent), Is.True);
            Assert.That(partitionComponent.Bucket, Is.EqualTo(1));
            Assert.That(partitionComponent.IsBehind, Is.True);
            Assert.That(partitionComponent.IsDirty, Is.True);
        }

        [Test]
        public void PartitionExistingEntity([Values(true, false)] bool isDirty)
        {
            samplingData.IsDirty.Returns(isDirty);
            samplingData.Forward.Returns(Vector3.forward);
            samplingData.Position.Returns(new Vector3(0, 0, 46)); // Partition #1
            samplingData.Parcel.Returns(ParcelMathHelper.FloorToParcel(new Vector3(0, 0, 46)));

            partitionSettings.SqrDistanceBuckets.Returns(new[] { 16 * 16, 32 * 32, 64 * 64 });

            Entity e = world.Create(
                new PartitionComponent { Bucket = 10, IsBehind = false },
                new SceneDefinitionComponent(new IpfsTypes.SceneEntityDefinition(), new[] { ParcelMathHelper.FloorToParcel(Vector3.zero) }, new IpfsTypes.IpfsPath()));

            system.Update(0);

            Assert.That(world.TryGet(e, out PartitionComponent partitionComponent), Is.True);
            Assert.That(partitionComponent.Bucket, Is.EqualTo(isDirty ? 1 : 10));
            Assert.That(partitionComponent.IsBehind, isDirty ? Is.True : Is.False);
            Assert.That(partitionComponent.IsDirty, isDirty ? Is.True : Is.False);
        }
    }
}
