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

        [SetUp]
        public void Setup()
        {
            var repoolableList = RepoolableList<WearableDTO>.NewList();
            var partialTargetList = repoolableList.List;
            partialTargetList.Capacity = 64;

            JsonConvert.PopulateObject(File.ReadAllText(definitionsPath), partialTargetList);
            wearableStorage = new WearableStorage();
            system = new LoadDefaultWearablesSystem(world, wearableStorage);

            system.Initialize();
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
