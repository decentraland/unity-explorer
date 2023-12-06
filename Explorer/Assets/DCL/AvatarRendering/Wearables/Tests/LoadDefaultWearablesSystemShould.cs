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
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Tests
{
    public class LoadDefaultWearablesSystemShould : UnitySystemTestBase<LoadDefaultWearablesSystem>
    {
        private readonly string definitionsPath = $"{Application.dataPath}/../TestResources/Wearables/DefaultWearableDefinition.txt";
        private WearableCatalog wearableCatalog;

        [SetUp]
        public void Setup()
        {
            var partialTargetList = new List<WearableDTO>(64);
            JsonConvert.PopulateObject(File.ReadAllText(definitionsPath), partialTargetList);

            system = new LoadDefaultWearablesSystem(world, new WearablesDTOList(partialTargetList), wearableCatalog = new WearableCatalog());
        }

        [Test]
        public void CreatePromisesForDefaultWearables()
        {
            system.Initialize();

            AssetPromise<IWearable[], GetWearablesByPointersIntention>[] promises = world.CacheDefaultWearablesState().GetDefaultWearablesState(world).PromisePerBodyShape;
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
            system.Initialize();

            ref readonly DefaultWearablesComponent state = ref world.CacheDefaultWearablesState().GetDefaultWearablesState(world);

            // resolve

            for (var i = 0; i < state.PromisePerBodyShape.Length; i++)
            {
                AssetPromise<IWearable[], GetWearablesByPointersIntention> promise = state.PromisePerBodyShape[i];
                world.Add(promise.Entity, new StreamableLoadingResult<IWearable[]>(Array.Empty<IWearable>()));
            }

            system.Update(0);

            for (var i = 0; i < state.PromisePerBodyShape.Length; i++)
            {
                AssetPromise<IWearable[], GetWearablesByPointersIntention> promise = state.PromisePerBodyShape[i];
                Assert.That(promise.IsConsumed, Is.True);
            }

            Assert.That(state.ResolvedState, Is.EqualTo(DefaultWearablesComponent.State.Success));
        }
    }
}
