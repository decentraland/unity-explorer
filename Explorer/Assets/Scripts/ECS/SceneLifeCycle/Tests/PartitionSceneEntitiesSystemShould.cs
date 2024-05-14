using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using DCL.Optimization.Pools;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.TestSuite;
using JetBrains.Annotations;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
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
        private PartitionSceneEntitiesSystemMock mockSystem;

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

            system = new PartitionSceneEntitiesSystemMock(world, componentPool, partitionSettings, samplingData, new PartitionDataContainer());
            mockSystem = system as PartitionSceneEntitiesSystemMock;
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
            samplingData.Position.Returns(new Vector3(0, 0, 46)); // Partition #1
            samplingData.Parcel.Returns(ParcelMathHelper.FloorToParcel(new Vector3(0, 0, 46)));

            var sceneDefinitionComponent = new SceneDefinitionComponent(new SceneEntityDefinition
            {
                    metadata = new SceneMetadata
                {
                        scene = new SceneMetadataScene
                        { DecodedParcels = new[] { ParcelMathHelper.FloorToParcel(Vector3.zero) } },
                },
            }, new IpfsPath()) { InternalJobIndex = 0 };

            Entity e = world.Create(new PartitionComponent { Bucket = 10, IsBehind = false }, sceneDefinitionComponent);
            mockSystem.AddPartitionData(0, ref sceneDefinitionComponent, new ScenesPartitioningUtils.PartitionData
            {
                Bucket = 10, IsBehind = false, IsDirty = true
            });

            system.Update(0);

            system.ForceCompleteJob();

            system.Update(0);

            Assert.That(world.TryGet(e, out PartitionComponent partitionComponent), Is.True);
            Assert.That(partitionComponent.Bucket, Is.EqualTo(1));
            Assert.That(partitionComponent.IsBehind, Is.True);
            Assert.That(partitionComponent.IsDirty, Is.True);
        }
    }

    public class PartitionSceneEntitiesSystemMock : PartitionSceneEntitiesSystem
    {
        internal PartitionSceneEntitiesSystemMock([NotNull] World world,
            [NotNull] IComponentPool<PartitionComponent> partitionComponentPool,
            [NotNull] IPartitionSettings partitionSettings,
            [NotNull] IReadOnlyCameraSamplingData readOnlyCameraSamplingData,
            PartitionDataContainer partitionDataContainer) : base(world, partitionComponentPool, partitionSettings, readOnlyCameraSamplingData, partitionDataContainer)
        {
        }

        public void AddPartitionData(int index, ref SceneDefinitionComponent sceneDefinitionComponent, ScenesPartitioningUtils.PartitionData data)
        {
            ScheduleSceneDefinition(ref sceneDefinitionComponent);
            partitionDataContainer.partitions[index] = data;
        }
    }
}
