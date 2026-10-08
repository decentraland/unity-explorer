using ECS;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SingleScene;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Utility;

namespace DCL.SceneLifeCycle.Tests
{
    public class StartSplittingByRingsSystemShould : UnitySystemTestBase<StartSplittingByRingsSystem>
    {
        private ParcelMathJobifiedHelper parcelMathJobifiedHelper = null!;
        private SingleSceneMode singleSceneMode = null!;
        private CameraSamplingData cameraSamplingData = null!;

        [SetUp]
        public void SetUp()
        {
            var realmPartitionSettings = Substitute.For<IRealmPartitionSettings>();
            realmPartitionSettings.MaxLoadingDistanceInParcels.Returns(2);

            parcelMathJobifiedHelper = new ParcelMathJobifiedHelper();
            singleSceneMode = new SingleSceneMode();
            singleSceneMode.SetActive(true);

            system = new StartSplittingByRingsSystem(world, realmPartitionSettings, parcelMathJobifiedHelper, singleSceneMode);

            cameraSamplingData = new CameraSamplingData();

            world.Create(new RealmComponent(new RealmData(new TestIpfsRealm())), ProcessedScenePointers.Create());
            world.Create(cameraSamplingData);
        }

        [Test]
        public void SplitAroundTheAnchorWhenTheCameraIsDirty()
        {
            singleSceneMode.SetAnchor(new Vector2Int(10, 11));
            cameraSamplingData.IsDirty = true;

            system.Update(0f);

            Assert.That(parcelMathJobifiedHelper.JobStarted, Is.True);
            parcelMathJobifiedHelper.Complete();
        }

        [Test]
        public void NotSplitWhileThereIsNoAnchor()
        {
            cameraSamplingData.IsDirty = true;

            system.Update(0f);

            Assert.That(parcelMathJobifiedHelper.JobStarted, Is.False);
        }

        [Test]
        public void NotSplitWhileTheCameraIsNotDirty()
        {
            singleSceneMode.SetAnchor(new Vector2Int(10, 11));

            system.Update(0f);

            Assert.That(parcelMathJobifiedHelper.JobStarted, Is.False);
        }

        [Test]
        public void SplitAroundTheCameraWhenSingleSceneModeIsInactive()
        {
            singleSceneMode.SetActive(false);
            cameraSamplingData.IsDirty = true;

            system.Update(0f);

            Assert.That(parcelMathJobifiedHelper.JobStarted, Is.True);
            parcelMathJobifiedHelper.Complete();
        }
    }
}
