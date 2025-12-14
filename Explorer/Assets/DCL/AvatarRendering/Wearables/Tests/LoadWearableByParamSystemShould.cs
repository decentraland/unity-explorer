using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Tests;
using DCL.AvatarRendering.AvatarShape.Tests.EditMode;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.Ipfs;
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
using LoadWearablesByParamSystem = DCL.AvatarRendering.Wearables.Systems.Load.LoadWearablesByParamSystem;

namespace DCL.AvatarRendering.Wearables.Tests
{
    [TestFixture]
    public class LoadWearableByParamSystemShould : LoadSystemBaseShould<LoadWearablesByParamSystem, WearablesResponse, GetWearableByParamIntention>
    {
        private TrimmedWearableStorage trimmedWearableStorage;
        private readonly string existingURN = "urn:decentraland:off-chain:base-avatars:aviatorstyle";

        private string successPath => $"file://{Application.dataPath}/../TestResources/Wearables/SuccessUserParam";

        private string failPath => $"file://{Application.dataPath}/../TestResources/Wearables/non_existing";
        private string wrongTypePath => $"file://{Application.dataPath + "/../TestResources/CRDT/arraybuffer.test"}";
        private int totalAmount => 0;

        protected override LoadWearablesByParamSystem CreateSystem()
        {
            trimmedWearableStorage = new TrimmedWearableStorage();

            IRealmData realmData = Substitute.For<IRealmData>();
            realmData.Configured.Returns(true);

            return new LoadWearablesByParamSystem(world, TestWebRequestController.INSTANCE, cache, realmData,
                URLSubdirectory.EMPTY, URLSubdirectory.FromString("Wearables"), URLDomain.EMPTY, new WearableStorage(), trimmedWearableStorage);
        }

        protected override void AssertSuccess(WearablesResponse asset)
        {
            base.AssertSuccess(asset);

            foreach (string wearableCatalogKey in trimmedWearableStorage.wearablesCache.Keys)
                Debug.Log(wearableCatalogKey);

            Assert.AreEqual(trimmedWearableStorage.wearablesCache.Count, 1);
            Assert.NotNull(trimmedWearableStorage.wearablesCache[existingURN]);
        }

        [Test]
        public async Task ConcludeSuccessOnExistingWearable()
        {
            var wearableDTO = new TrimmedWearableDTO();
            wearableDTO.id = existingURN;
            wearableDTO.assetBundleManifestVersion = AssetBundleManifestVersion.CreateFromFallback("v18", "2024-05-01T05:41:08.138Z");

            trimmedWearableStorage.wearablesCache.Add(existingURN, new FakeTrimmedWearable(wearableDTO));
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

            return new GetWearableByParamIntention(Array.Empty<(string, string)>(), successPath, new List<ITrimmedWearable>(), totalAmount);
        }

        protected override GetWearableByParamIntention CreateNotFoundIntention() =>
            new (Array.Empty<(string, string)>(), failPath, new List<ITrimmedWearable>(), totalAmount);

        protected override GetWearableByParamIntention CreateWrongTypeIntention() =>
            new (Array.Empty<(string, string)>(), wrongTypePath, new List<ITrimmedWearable>(), totalAmount);
    }
}
