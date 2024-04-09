using Arch.Core;
using CrdtEcsBridge.Components.Special;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.Systems;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace ECS.Unity.Tests
{
    public class PartitionAssetEntitiesSystemShould : UnitySystemTestBase<PartitionAssetEntitiesSystem>
    {
        private Entity sceneRoot;
        private IPartitionSettings partitionSettings;
        private IPartitionComponent scenePartition;
        private IReadOnlyCameraSamplingData samplingData;
        private IComponentPool<PartitionComponent> componentPool;

        private ref TransformComponent sceneRootTransform => ref world.Get<TransformComponent>(sceneRoot);


        public void SetUp()
        {
            sceneRoot = world.Create(new SceneRootComponent());
            AddTransformToEntity(sceneRoot);
            partitionSettings = Substitute.For<IPartitionSettings>();
            partitionSettings.AngleTolerance.Returns(0);
            partitionSettings.PositionSqrTolerance.Returns(0);
            partitionSettings.FastPathSqrDistance.Returns(int.MaxValue);

            samplingData = Substitute.For<IReadOnlyCameraSamplingData>();
            componentPool = Substitute.For<IComponentPool<PartitionComponent>>();
            componentPool.Get().Returns(_ => new PartitionComponent());
            scenePartition = Substitute.For<IPartitionComponent>();

            system = new PartitionAssetEntitiesSystem(world, partitionSettings, scenePartition, samplingData, componentPool, sceneRoot);
        }


        public void RepartitionExistingEntity([Values(true, false)] bool isDirty)
        {
            samplingData.IsDirty.Returns(isDirty);
            samplingData.Forward.Returns(Vector3.forward);
            samplingData.Position.Returns(new Vector3(0, 0, 26)); // Partition #1
            partitionSettings.SqrDistanceBuckets.Returns(new[] { 16 * 16, 32 * 32, 64 * 64 });

            Entity e = world.Create(new PartitionComponent { Bucket = 10, IsBehind = false }, new PBGltfContainer());
            TransformComponent t = AddTransformToEntity(e);

            t.Transform.position = Vector3.zero;
            t.Transform.forward = Vector3.forward;

            system.Update(0);

            Assert.That(world.TryGet(e, out PartitionComponent partitionComponent), Is.True);
            Assert.That(partitionComponent.Bucket, Is.EqualTo(isDirty ? 1 : 10));
            Assert.That(partitionComponent.IsBehind, isDirty ? Is.True : Is.False);
            Assert.That(partitionComponent.IsDirty, isDirty ? Is.True : Is.False);
        }


        public void RepartitionExistingEntityWithoutTransform([Values(true, false)] bool isDirty)
        {
            samplingData.IsDirty.Returns(isDirty);
            samplingData.Forward.Returns(Vector3.forward);
            samplingData.Position.Returns(new Vector3(0, 0, 26)); // Partition #1
            partitionSettings.SqrDistanceBuckets.Returns(new[] { 16 * 16, 32 * 32, 64 * 64 });

            Entity e = world.Create(new PartitionComponent { Bucket = 10, IsBehind = false }, new PBGltfContainer());

            sceneRootTransform.SetTransform(Vector3.zero, Quaternion.identity, Vector3.one);

            system.Update(0);

            Assert.That(world.TryGet(e, out PartitionComponent partitionComponent), Is.True);
            Assert.That(partitionComponent.Bucket, Is.EqualTo(isDirty ? 1 : 10));
            Assert.That(partitionComponent.IsBehind, isDirty ? Is.True : Is.False);
            Assert.That(partitionComponent.IsDirty, isDirty ? Is.True : Is.False);
        }


        public void PartitionNewEntity([Values(true, false)] bool isDirty)
        {
            samplingData.IsDirty.Returns(isDirty);
            samplingData.Forward.Returns(Vector3.forward);
            samplingData.Position.Returns(new Vector3(0, 0, 26)); // Partition #1
            partitionSettings.SqrDistanceBuckets.Returns(new[] { 16 * 16, 32 * 32, 64 * 64 });

            Entity e = world.Create(new PBGltfContainer());
            TransformComponent t = AddTransformToEntity(e, true);

            t.Transform.position = Vector3.zero;

            system.Update(0);

            Assert.That(world.TryGet(e, out PartitionComponent partitionComponent), Is.True);
            Assert.That(partitionComponent.Bucket, Is.EqualTo(1));
            Assert.That(partitionComponent.IsBehind, Is.True);
            Assert.That(partitionComponent.IsDirty, Is.True);
        }


        public void PartitionNewEntityWithoutTransform([Values(true, false)] bool isDirty)
        {
            samplingData.IsDirty.Returns(isDirty);
            samplingData.Forward.Returns(Vector3.forward);
            samplingData.Position.Returns(new Vector3(0, 0, 100)); // Partition #3
            partitionSettings.SqrDistanceBuckets.Returns(new[] { 16 * 16, 32 * 32, 64 * 64 });

            Entity e = world.Create(new PBGltfContainer());

            sceneRootTransform.SetTransform(new Vector3(0, 0, 180), Quaternion.identity, Vector3.one);

            system.Update(0);

            Assert.That(world.TryGet(e, out PartitionComponent partitionComponent), Is.True);
            Assert.That(partitionComponent.Bucket, Is.EqualTo(3));
            Assert.That(partitionComponent.IsBehind, Is.False);
            Assert.That(partitionComponent.IsDirty, Is.True);
        }


        public void InheritScenePartition()
        {
            samplingData.IsDirty.Returns(true);
            samplingData.Forward.Returns(Vector3.forward);
            samplingData.Position.Returns(new Vector3(100, 0, 0)); // distance = 100
            partitionSettings.SqrDistanceBuckets.Returns(new[] { 16 * 16, 32 * 32, 64 * 64 });

            Entity e = world.Create(new PBGltfContainer());
            TransformComponent t = AddTransformToEntity(e, true);

            t.Transform.position = Vector3.zero;

            partitionSettings.FastPathSqrDistance.Returns(10); // 100 > 10
            scenePartition.Bucket.Returns((byte)20);
            scenePartition.IsBehind.Returns(false);

            system.Update(0);

            Assert.That(world.TryGet(e, out PartitionComponent partitionComponent), Is.True);
            Assert.That(partitionComponent.Bucket, Is.EqualTo(20));
            Assert.That(partitionComponent.IsBehind, Is.False);
            Assert.That(partitionComponent.IsDirty, Is.True);
        }
    }
}
