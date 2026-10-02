using Arch.Core;
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

        [SetUp]
        public void SetUp()
        {
            var realmPartitionSettings = Substitute.For<IRealmPartitionSettings>();
            realmPartitionSettings.MaxLoadingDistanceInParcels.Returns(2);

            parcelMathJobifiedHelper = new ParcelMathJobifiedHelper();
            singleSceneMode = new SingleSceneMode();
            singleSceneMode.SetActive(true);

            system = new StartSplittingByRingsSystem(world, realmPartitionSettings, parcelMathJobifiedHelper, singleSceneMode);

            world.Create(new RealmComponent(new RealmData(new TestIpfsRealm())), ProcessedScenePointers.Create());
            world.Create(new CameraSamplingData());
        }

        [Test]
        public void SplitOnlyOnceWhileTheAnchorDoesNotMove()
        {
            singleSceneMode.SetAnchor(new Vector2Int(10, 11));

            system.Update(0f);

            Assert.That(parcelMathJobifiedHelper.JobStarted, Is.True);
            parcelMathJobifiedHelper.Complete();

            system.Update(0f);

            Assert.That(parcelMathJobifiedHelper.JobStarted, Is.False);
        }

        [Test]
        public void SplitAgainOnRealmChangeWhenTheAnchorIsUnchanged()
        {
            singleSceneMode.SetAnchor(new Vector2Int(10, 11));

            system.Update(0f);
            parcelMathJobifiedHelper.Complete();

            system.FinalizeComponents(world.Query(new QueryDescription()));

            system.Update(0f);

            Assert.That(parcelMathJobifiedHelper.JobStarted, Is.True);
            parcelMathJobifiedHelper.Complete();
        }

        [Test]
        public void NotSplitWhileThereIsNoAnchor()
        {

            system.Update(0f);

            Assert.That(parcelMathJobifiedHelper.JobStarted, Is.False);
        }
    }
}
