using AssetManagement;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using DCL.Optimization.PerformanceBudgeting;
using NSubstitute;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Tests
{
    public class LoadDefaultWearablesSystemShould : UnitySystemTestBase<LoadDefaultWearablesSystem>
    {
        private readonly string definitionsPath = $"{Application.dataPath}/../TestResources/Wearables/DefaultWearableDefinition.txt";

        private WearableCatalog wearableCatalog;
        private GameObject emptyDefaultWearable;

        [SetUp]
        public void Setup()
        {
            var partialTargetList = new List<WearableDTO>(64);
            JsonConvert.PopulateObject(File.ReadAllText(definitionsPath), partialTargetList);
            wearableCatalog = new WearableCatalog();
            emptyDefaultWearable = new GameObject();

            system = new LoadDefaultWearablesSystem(world, new WearablesDTOList(partialTargetList),
                emptyDefaultWearable,
                wearableCatalog);

            system.Initialize();
        }

        [Test]
        public void CreatePromisesForDefaultWearables()
        {
            AssetPromise<WearablesResolution, GetWearablesByPointersIntention>[] promises = world.CacheDefaultWearablesState().GetDefaultWearablesState(world).PromisePerBodyShape;
            Assert.That(promises.Length, Is.EqualTo(BodyShape.COUNT));

            for (var i = 0; i < promises.Length; i++)
            {
                Assert.That(promises[i].LoadingIntention.Pointers.Count, Is.GreaterThan(0));
                Assert.That(promises[i].LoadingIntention.PermittedSources, Is.EqualTo(AssetSource.EMBEDDED));
                Assert.That(promises[i].LoadingIntention.FallbackToDefaultWearables, Is.EqualTo(false));
            }
        }

        [Test]
        public void ConsumePromises()
        {
            ref readonly DefaultWearablesComponent state = ref world.CacheDefaultWearablesState().GetDefaultWearablesState(world);

            // resolve

            for (var i = 0; i < state.PromisePerBodyShape.Length; i++)
            {
                AssetPromise<WearablesResolution, GetWearablesByPointersIntention> promise = state.PromisePerBodyShape[i];
                world.Add(promise.Entity, new StreamableLoadingResult<WearablesResolution>(WearablesResolution.EMPTY));
            }

            system.Update(0);

            for (var i = 0; i < state.PromisePerBodyShape.Length; i++)
            {
                AssetPromise<WearablesResolution, GetWearablesByPointersIntention> promise = state.PromisePerBodyShape[i];
                Assert.That(promise.IsConsumed, Is.True);
            }

            Assert.That(state.ResolvedState, Is.EqualTo(DefaultWearablesComponent.State.Success));
        }

        [Test]
        public void LoadEmptyDefaultWearable()
        {
            //Look for an empty and a non-empty default wearable
            IWearable tiaraDefaultWearable =
                wearableCatalog.GetDefaultWearable(BodyShape.MALE, WearablesConstants.Categories.TIARA,
                    out bool shouldBeEmpty);

            IWearable upperBodyDefaultWearable =
                wearableCatalog.GetDefaultWearable(BodyShape.MALE, WearablesConstants.Categories.UPPER_BODY,
                    out bool shouldntBeEmpty);

            Assert.AreEqual(((WearableRegularAsset)tiaraDefaultWearable.WearableAssetResults[BodyShape.MALE].Results[0].Value.Asset).MainAsset,
                emptyDefaultWearable);

            Assert.AreEqual(tiaraDefaultWearable.GetUrn().ToString(), WearablesConstants.EMPTY_DEFAULT_WEARABLE);
            Assert.IsTrue(shouldBeEmpty);

            // In this test suite we are not loading the default wearables through the LoadAssetBundleSystem.
            // So, to confirm that the default wearable is not loaded, we check that the asset is null and that the urn is not from the empty default wearable
            // Results are not created as it's not run through the system
            Assert.AreEqual(upperBodyDefaultWearable.WearableAssetResults[BodyShape.MALE].Results, null);
            Assert.AreNotEqual(upperBodyDefaultWearable.GetUrn(), WearablesConstants.EMPTY_DEFAULT_WEARABLE);
            Assert.IsFalse(shouldntBeEmpty);
        }

        [Test]
        public void HasUnloadPolicySet()
        {
            int defaultWearableCount = wearableCatalog.wearablesCache.Keys.Count;
            wearableCatalog.AddEmptyWearable("Wearable_To_Be_Unloaded");
            Assert.AreEqual(wearableCatalog.wearablesCache.Keys.Count, defaultWearableCount + 1);

            IReleasablePerformanceBudget concurrentBudgetProvider = Substitute.For<IReleasablePerformanceBudget>();
            concurrentBudgetProvider.TrySpendBudget().Returns(true);
            wearableCatalog.Unload(concurrentBudgetProvider);

            Assert.AreEqual(wearableCatalog.wearablesCache.Keys.Count, defaultWearableCount);
        }
    }
}
