﻿using Arch.Core;
using DCL.Ipfs;
using DCL.Utilities;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.Reporting;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Tests
{
    public class LoadPointersByIncreasingRadiusSystemShould : UnitySystemTestBase<LoadPointersByIncreasingRadiusSystem>
    {
        private ParcelMathJobifiedHelper parcelMathJobifiedHelper;
        private IRealmPartitionSettings realmPartitionSettings;
        private IPartitionSettings partitionSettings;


        [SetUp]
        public void SetUp()
        {
            IRealmData realmData = Substitute.For<IRealmData>();
            realmData.RealmType.Returns(new ReactiveProperty<RealmKind>(RealmKind.GenesisCity));
            system = new LoadPointersByIncreasingRadiusSystem(world,
                parcelMathJobifiedHelper = new ParcelMathJobifiedHelper(),
                realmPartitionSettings = Substitute.For<IRealmPartitionSettings>(),
                partitionSettings = Substitute.For<IPartitionSettings>(),
                Substitute.For<ISceneReadinessReportQueue>(),
                Substitute.For<IScenesCache>(),
                new HashSet<Vector2Int>(), realmData);

            realmPartitionSettings.ScenesDefinitionsRequestBatchSize.Returns(3000);
        }

        [TearDown]
        public void TearDown()
        {
            parcelMathJobifiedHelper.Dispose();
        }

        [Test]
        public void StartLoading([NUnit.Framework.Range(1, 10, 1)] int radius)
        {
            var realm = new RealmComponent(new RealmData(new TestIpfsRealm()));
            using var processedParcels = new NativeHashSet<int2>(100, AllocatorManager.Persistent);

            parcelMathJobifiedHelper.StartParcelsRingSplit(new int2(1, 1), radius, processedParcels);

            var scenePointers = new VolatileScenePointers(new List<SceneEntityDefinition>(),
                new List<int2>(), new PartitionComponent());

            Entity e = world.Create(realm, scenePointers, new ProcessedScenePointers { Value = processedParcels });
            system.Update(0);

            Assert.That(parcelMathJobifiedHelper.JobStarted, Is.False);

            int d = (radius * 2) + 1;
            scenePointers = world.Get<VolatileScenePointers>(e);

            Assert.That(scenePointers.ActivePromise.HasValue, Is.True);
            Assert.That(scenePointers.ActivePromise.Value.LoadingIntention.Pointers.Count, Is.EqualTo(d * d));
        }

        [Test]
        public void NotStartLoadingProcessedParcels([NUnit.Framework.Range(1, 10, 1)] int radius)
        {
            var realm = new RealmComponent(new RealmData(new TestIpfsRealm()));
            using var processedParcels = new NativeHashSet<int2>(100, AllocatorManager.Persistent);

            parcelMathJobifiedHelper.StartParcelsRingSplit(new int2(1, 1), radius, processedParcels);
            ref readonly NativeArray<ParcelMathJobifiedHelper.ParcelInfo> array = ref parcelMathJobifiedHelper.FinishParcelsRingSplit();

            // add all parcels to processed
            foreach (ParcelMathJobifiedHelper.ParcelInfo parcel in array)
                processedParcels.Add(parcel.Parcel);

            var scenePointers = new VolatileScenePointers(new List<SceneEntityDefinition>(),
                new List<int2>(), new PartitionComponent());

            Entity e = world.Create(realm, scenePointers, new ProcessedScenePointers { Value = processedParcels });

            // For system
            parcelMathJobifiedHelper.StartParcelsRingSplit(new int2(1, 1), radius, processedParcels);

            system.Update(0);

            Assert.That(parcelMathJobifiedHelper.JobStarted, Is.False);
            scenePointers = world.Get<VolatileScenePointers>(e);
            Assert.That(scenePointers.ActivePromise.HasValue, Is.False);
        }
    }
}
