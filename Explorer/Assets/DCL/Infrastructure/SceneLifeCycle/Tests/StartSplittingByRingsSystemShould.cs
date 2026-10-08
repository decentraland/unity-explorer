using ECS;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SingleScene;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using Unity.Mathematics;
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
            // Distinct from the anchor, so the centre ring tells the two apart
            cameraSamplingData.Parcel = new Vector2Int(1, 2);
            singleSceneMode.SetAnchor(new Vector2Int(10, 11));
            cameraSamplingData.IsDirty = true;

            system.Update(0f);

            Assert.That(parcelMathJobifiedHelper.JobStarted, Is.True);

            parcelMathJobifiedHelper.FinishParcelsRingSplit();

            Assert.That(parcelMathJobifiedHelper.GetRing(0)[0].Parcel, Is.EqualTo(new int2(10, 11)));
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
            singleSceneMode.SetAnchor(new Vector2Int(10, 11));
            cameraSamplingData.Parcel = new Vector2Int(1, 2);
            cameraSamplingData.IsDirty = true;

            system.Update(0f);

            Assert.That(parcelMathJobifiedHelper.JobStarted, Is.True);

            parcelMathJobifiedHelper.FinishParcelsRingSplit();

            Assert.That(parcelMathJobifiedHelper.GetRing(0)[0].Parcel, Is.EqualTo(new int2(1, 2)));
        }
    }
}
