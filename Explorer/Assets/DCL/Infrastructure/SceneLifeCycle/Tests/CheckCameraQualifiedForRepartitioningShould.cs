using Arch.Core;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using ECS;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Systems;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.SceneLifeCycle.Tests
{
    public class CheckCameraQualifiedForRepartitioningShould : UnitySystemTestBase<CheckCameraQualifiedForRepartitioningSystem>
    {
        private IPartitionSettings partitionSettings = null!;
        private CameraSamplingData sharedCameraSamplingData = null!;

        [SetUp]
        public void SetUp()
        {
            partitionSettings = Substitute.For<IPartitionSettings>();
            var realmData = new RealmData(new TestIpfsRealm());
            sharedCameraSamplingData = new CameraSamplingData { Position = new Vector3(10, 10, 10) };
            system = new CheckCameraQualifiedForRepartitioningSystem(world, partitionSettings, realmData, sharedCameraSamplingData);

            world.Create(new RealmComponent(realmData));
        }

        [Test]
        public void SampleTheTeleportDestinationWhenItTravelsInThePosition()
        {
            // The realm change and movePlayerTo paths leave Parcel at zero and carry the destination in Position
            Vector3 destination = ParcelMathHelper.GetPositionByParcelPosition(new Vector2Int(7, 3));

            world.Create(new PlayerTeleportIntent(null, Vector2Int.zero, destination, CancellationToken.None, isPositionSet: true));

            system.Update(0);

            Assert.That(sharedCameraSamplingData.Parcel, Is.EqualTo(new Vector2Int(7, 3)));
            Assert.That(sharedCameraSamplingData.IsDirty, Is.True);
        }

        [Test]
        public void SampleTheTeleportParcelWhenThePositionIsNotSet()
        {
            world.Create(new PlayerTeleportIntent(null, new Vector2Int(5, 5), Vector3.zero, CancellationToken.None));

            system.Update(0);

            Assert.That(sharedCameraSamplingData.Parcel, Is.EqualTo(new Vector2Int(5, 5)));
        }

        [Test]
        public void ToleratePositionChange()
        {
            partitionSettings.AngleTolerance.Returns(float.MaxValue);
            partitionSettings.PositionSqrTolerance.Returns(10 * 10);

            Transform cameraTransform = new GameObject("Camera").transform;

            Entity camera = world.Create(
                new CameraComponent(cameraTransform.gameObject.AddComponent<Camera>()),
                new CameraSamplingData { Position = new Vector3(10, 10, 10) });

            cameraTransform.position = new Vector3(12, 11, 11); // less than 10 units away

            system?.Update(0);

            Assert.That(world.TryGet(camera, out CameraSamplingData? cameraSamplingData), Is.True);
            Assert.That(cameraSamplingData!.IsDirty, Is.False);
            Assert.That(cameraSamplingData.Position, Is.EqualTo(new Vector3(10, 10, 10)));
        }

        [Test]
        public void RespectPositionChange()
        {
            partitionSettings.AngleTolerance.Returns(float.MaxValue);
            partitionSettings.PositionSqrTolerance.Returns(1);

            Transform cameraTransform = new GameObject("Camera").transform;

            Entity camera = world.Create(
                new CameraComponent(cameraTransform.gameObject.AddComponent<Camera>()),
                new CameraSamplingData { Position = new Vector3(10, 10, 10) });

            cameraTransform.position = new Vector3(12, 11, 11); // more than 1 unit away

            system?.Update(0);

            Assert.That(world.TryGet(camera, out CameraSamplingData? cameraSamplingData), Is.True);
            Assert.That(cameraSamplingData!.IsDirty, Is.True);
            Assert.That(cameraSamplingData.Position, Is.EqualTo(new Vector3(12, 11, 11)));
        }

        [Test]
        public void TolerateRotationChange()
        {
            partitionSettings.AngleTolerance.Returns(10f); // 10 degrees
            partitionSettings.PositionSqrTolerance.Returns(float.MaxValue);

            Transform cameraTransform = new GameObject("Camera").transform;

            Entity camera = world.Create(
                new CameraComponent(cameraTransform.gameObject.AddComponent<Camera>()),
                new CameraSamplingData { Rotation = Quaternion.Euler(25, 25, 25), Position = Vector3.zero });

            cameraTransform.rotation = Quaternion.Euler(20, 25, 25); // less than 10 degrees

            system?.Update(0);

            Assert.That(world.TryGet(camera, out CameraSamplingData? cameraSamplingData), Is.True);
            Assert.That(cameraSamplingData!.IsDirty, Is.False);
            Assert.That(cameraSamplingData.Rotation, Is.EqualTo(Quaternion.Euler(25, 25, 25)));
        }

        [Test]
        public void RespectRotationChange()
        {
            partitionSettings.AngleTolerance.Returns(10f); // 10 degrees
            partitionSettings.PositionSqrTolerance.Returns(float.MaxValue);

            Transform cameraTransform = new GameObject("Camera").transform;

            Entity camera = world.Create(
                new CameraComponent(cameraTransform.gameObject.AddComponent<Camera>()),
                new CameraSamplingData { Rotation = Quaternion.Euler(25, 25, 25), Position = Vector3.zero });

            cameraTransform.rotation = Quaternion.Euler(20, 40, 25); // more than 10 degrees

            system?.Update(0);

            Assert.That(world.TryGet(camera, out CameraSamplingData? cameraSamplingData), Is.True);
            Assert.That(cameraSamplingData!.IsDirty, Is.True);
            Assert.That(cameraSamplingData.Rotation.eulerAngles.x, Is.EqualTo(20f).Within(1).Percent);
            Assert.That(cameraSamplingData.Rotation.eulerAngles.y, Is.EqualTo(40f).Within(1).Percent);
            Assert.That(cameraSamplingData.Rotation.eulerAngles.z, Is.EqualTo(25f).Within(1).Percent);
        }
    }
}
