﻿using Arch.Core;
using DCL.Ipfs;
using DCL.Optimization.Pools;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
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

            partitionSettings.SqrDistanceBuckets.Returns(
                new List<int> { 16 * 16, 32 * 32, 64 * 64 });

            samplingData = Substitute.For<IReadOnlyCameraSamplingData>();
            componentPool = Substitute.For<IComponentPool<PartitionComponent>>();
            componentPool.Get().Returns(_ => new PartitionComponent());
            IRealmPartitionSettings realmPartitionSettings = Substitute.For<IRealmPartitionSettings>();

            system = new PartitionSceneEntitiesSystem(world, componentPool, partitionSettings,
                samplingData, realmPartitionSettings);
        }

        [Test]
        public void PartitionNewEntity()
        {
            samplingData.IsDirty.Returns(true);
            samplingData.Forward.Returns(Vector3.forward);
            samplingData.Position.Returns(new Vector3(0, 0, 46)); // Partition #1
            samplingData.Parcel.Returns(new Vector3(0, 0, 46).ToParcel());

            Entity e = world.Create(SceneDefinitionComponentFactory.CreateFromDefinition(new SceneEntityDefinition
            {
                metadata = new SceneMetadata
                {
                    scene = new SceneMetadataScene
                        { DecodedParcels = new[] { Vector3.zero.ToParcel() } },
                },
            }, new IpfsPath()));

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
            samplingData.Parcel.Returns(coords.ToParcel());

            // new entity without partition
            Entity e = world.Create(SceneDefinitionComponentFactory.CreateFromDefinition(new SceneEntityDefinition
            {
                metadata = new SceneMetadata
                {
                    scene = new SceneMetadataScene
                        { DecodedParcels = new[] { Vector3.zero.ToParcel() } },
                },
            }, new IpfsPath()));

            // Run for the first time so the internals of the system change
            system.Update(0);

            // Move to another partition
            coords = new Vector3(0, 0, 46);

            samplingData.Position.Returns(coords); // Partition #1
            samplingData.Parcel.Returns(coords.ToParcel());

            // Run for the second time
            system.Update(0);

            // Partition should be set to the proper values

            Assert.That(world.TryGet(e, out PartitionComponent partitionComponent), Is.True);
            Assert.That(partitionComponent.Bucket, Is.EqualTo(1));
            Assert.That(partitionComponent.IsBehind, Is.True);
            Assert.That(partitionComponent.IsDirty, Is.True);
        }
    }
}
