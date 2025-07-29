using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Optimization.PerformanceBudgeting;
using ECS.TestSuite;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using System.IO;
using UnityEngine;
using LoadDefaultWearablesSystem = DCL.AvatarRendering.Wearables.Systems.Load.LoadDefaultWearablesSystem;

namespace DCL.AvatarRendering.Wearables.Tests
{
    public class LoadDefaultWearablesSystemShould : UnitySystemTestBase<LoadDefaultWearablesSystem>
    {
        private readonly string definitionsPath = $"{Application.dataPath}/../TestResources/Wearables/DefaultWearableDefinition.txt";

        private WearableStorage wearableStorage;
        private GameObject emptyDefaultWearable;

        [SetUp]
        public void Setup()
        {
            var repoolableList = RepoolableList<WearableDTO>.NewList();
            var partialTargetList = repoolableList.List;
            partialTargetList.Capacity = 64;

            JsonConvert.PopulateObject(File.ReadAllText(definitionsPath), partialTargetList);
            wearableStorage = new WearableStorage();
            emptyDefaultWearable = new GameObject();

            system = new LoadDefaultWearablesSystem(world,
                emptyDefaultWearable,
                wearableStorage);

            system.Initialize();
        }

        [Test]
        public void LoadEmptyDefaultWearable()
        {
            //Look for an empty and a non-empty default wearable
            IWearable tiaraDefaultWearable =
                wearableStorage.GetDefaultWearable(BodyShape.MALE, WearablesConstants.Categories.TIARA);

            IWearable upperBodyDefaultWearable =
                wearableStorage.GetDefaultWearable(BodyShape.MALE, WearablesConstants.Categories.UPPER_BODY);

            Assert.AreEqual(((AttachmentRegularAsset)tiaraDefaultWearable.WearableAssetResults[BodyShape.MALE].Results[0].Value.Asset).MainAsset,
                emptyDefaultWearable);

            Assert.AreEqual(tiaraDefaultWearable.GetUrn().ToString(), WearablesConstants.EMPTY_DEFAULT_WEARABLE);

            // In this test suite we are not loading the default wearables through the LoadAssetBundleSystem.
            // So, to confirm that the default wearable is not loaded, we check that the asset is null and that the urn is not from the empty default wearable
            // Results are not created as it's not run through the system
            Assert.AreEqual(upperBodyDefaultWearable.WearableAssetResults[BodyShape.MALE].Results, null);
            Assert.AreNotEqual(upperBodyDefaultWearable.GetUrn(), WearablesConstants.EMPTY_DEFAULT_WEARABLE);
        }

        [Test]
        public void HasUnloadPolicySet()
        {
            int defaultWearableCount = wearableStorage.wearablesCache.Keys.Count;
            wearableStorage.AddWearable("Wearable_To_Be_Unloaded", IWearable.NewEmpty(), true);
            Assert.AreEqual(wearableStorage.wearablesCache.Keys.Count, defaultWearableCount + 1);

            IReleasablePerformanceBudget concurrentBudgetProvider = Substitute.For<IReleasablePerformanceBudget>();
            concurrentBudgetProvider.TrySpendBudget().Returns(true);
            wearableStorage.Unload(concurrentBudgetProvider);

            Assert.AreEqual(wearableStorage.wearablesCache.Keys.Count, defaultWearableCount);
        }
    }
}
