using Arch.Core;
using DCL.Ipfs;
using DCL.Optimization.Pools;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using ECS.SceneLifeCycle.Components;
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
            partitionSettings.SqrDistanceBuckets.Returns(new[] { 16 * 16, 32 * 32, 64 * 64 });

            samplingData = Substitute.For<IReadOnlyCameraSamplingData>();
            componentPool = Substitute.For<IComponentPool<PartitionComponent>>();
            componentPool.Get().Returns(_ => new PartitionComponent());
            var realmPartitionSettings = Substitute.For<IRealmPartitionSettings>();

            system = new PartitionSceneEntitiesSystem(world, null, componentPool, partitionSettings, samplingData, new PartitionDataContainer(), realmPartitionSettings);
            system.partitionDataContainer.Restart();
        }

        [Test]
        public void PartitionNewEntity()
        {
            samplingData.IsDirty.Returns(true);
            samplingData.Forward.Returns(Vector3.forward);
            samplingData.Position.Returns(new Vector3(0, 0, 46)); // Partition #1
            samplingData.Parcel.Returns(ParcelMathHelper.FloorToParcel(new Vector3(0, 0, 46)));

            Entity e = world.Create(new SceneDefinitionComponent(new SceneEntityDefinition
            {
                metadata = new SceneMetadata
                {
                    scene = new SceneMetadataScene
                        { DecodedParcels = new[] { ParcelMathHelper.FloorToParcel(Vector3.zero) } },
                },
            }, new IpfsPath()));

            system.Update(0);

            system.ForceCompleteJob();

            system.Update(0);

            Assert.That(world.TryGet(e, out PartitionComponent partitionComponent), Is.True);
            Assert.That(partitionComponent.Bucket, Is.EqualTo(1));
            Assert.That(partitionComponent.IsBehind, Is.True);
            Assert.That(partitionComponent.IsDirty, Is.True);
        }

        [Test]
        public void PartitionExistingEntity()
        {
            samplingData.IsDirty.Returns(true);
            samplingData.Forward.Returns(Vector3.forward);

            var coords = new Vector3(0, 0, 100);

            samplingData.Position.Returns(coords); // Partition #3
            samplingData.Parcel.Returns(ParcelMathHelper.FloorToParcel(coords));

            // new entity without partition
            Entity e = world.Create(new SceneDefinitionComponent(new SceneEntityDefinition
            {
                metadata = new SceneMetadata
                {
                    scene = new SceneMetadataScene
                        { DecodedParcels = new[] { ParcelMathHelper.FloorToParcel(Vector3.zero) } },
                },
            }, new IpfsPath()));


            // Run for the first time so the internals of the system change
            system.Update(0);
            system.ForceCompleteJob();

            // Move to another partition
            coords = new Vector3(0, 0, 46);

            samplingData.Position.Returns(coords); // Partition #1
            samplingData.Parcel.Returns(ParcelMathHelper.FloorToParcel(coords));

            // Run for the second time
            system.Update(0);
            system.ForceCompleteJob();
            system.Update(0);

            // Partition should be set to the proper values

            Assert.That(world.TryGet(e, out PartitionComponent partitionComponent), Is.True);
            Assert.That(partitionComponent.Bucket, Is.EqualTo(1));
            Assert.That(partitionComponent.IsBehind, Is.True);
            Assert.That(partitionComponent.IsDirty, Is.True);
        }
    }
}
