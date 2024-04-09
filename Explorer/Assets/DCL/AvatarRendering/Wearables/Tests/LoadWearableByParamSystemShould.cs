using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using ECS;
using ECS.StreamableLoading.Tests;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables.Tests
{

    public class LoadWearableByParamSystemShould : LoadSystemBaseShould<LoadWearablesByParamSystem, WearablesResponse, GetWearableByParamIntention>
    {
        private WearableCatalog wearableCatalog;
        private readonly string existingURN = "urn:decentraland:off-chain:base-avatars:aviatorstyle";

        private string successPath => $"file://{Application.dataPath}/../TestResources/Wearables/SuccessUserParam";

        private string failPath => $"file://{Application.dataPath}/../TestResources/Wearables/non_existing";
        private string wrongTypePath => $"file://{Application.dataPath + "/../TestResources/CRDT/arraybuffer.test"}";
        private int totalAmount => 0;

        protected override LoadWearablesByParamSystem CreateSystem()
        {
            wearableCatalog = new WearableCatalog();

            IRealmData realmData = Substitute.For<IRealmData>();
            realmData.Configured.Returns(true);

            return new LoadWearablesByParamSystem(world, TestWebRequestController.INSTANCE, cache, realmData,
                URLSubdirectory.EMPTY, URLSubdirectory.FromString("Wearables"), wearableCatalog, new MutexSync());
        }

        protected override void AssertSuccess(WearablesResponse asset)
        {
            base.AssertSuccess(asset);

            foreach (string wearableCatalogKey in wearableCatalog.wearablesCache.Keys)
                Debug.Log(wearableCatalogKey);

            Assert.AreEqual(wearableCatalog.wearablesCache.Count, 1);
            Assert.NotNull(wearableCatalog.wearablesCache[existingURN]);
        }


        public async Task ConcludeSuccessOnExistingWearable()
        {
            wearableCatalog.wearablesCache.Add(existingURN, Substitute.For<IWearable>());
            await ConcludeSuccess();
        }

        protected override GetWearableByParamIntention CreateSuccessIntention()
        {
            IURLBuilder urlBuilder = Substitute.For<IURLBuilder>();
            urlBuilder.AppendDomainWithReplacedPath(Arg.Any<URLDomain>(), Arg.Any<URLSubdirectory>()).Returns(urlBuilder);
            urlBuilder.AppendSubDirectory(Arg.Any<URLSubdirectory>()).Returns(urlBuilder);
            urlBuilder.GetResult().Returns(successPath);
            urlBuilder.Build().Returns(URLAddress.FromString(successPath));

            system.urlBuilder = urlBuilder;

            return new GetWearableByParamIntention(Array.Empty<(string, string)>(), successPath, new List<IWearable>(), totalAmount);
        }

        protected override GetWearableByParamIntention CreateNotFoundIntention() =>
            new (Array.Empty<(string, string)>(), failPath, new List<IWearable>(), totalAmount);

        protected override GetWearableByParamIntention CreateWrongTypeIntention() =>
            new (Array.Empty<(string, string)>(), wrongTypePath, new List<IWearable>(), totalAmount);
    }
}
