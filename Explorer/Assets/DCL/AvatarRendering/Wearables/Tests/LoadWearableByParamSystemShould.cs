using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.WebRequests;
using ECS;
using ECS.StreamableLoading.Tests;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using LoadWearablesByParamSystem = DCL.AvatarRendering.Wearables.Systems.Load.LoadWearablesByParamSystem;

namespace DCL.AvatarRendering.Wearables.Tests
{
    [TestFixture(WebRequestsMode.UNITY)]
    [TestFixture(WebRequestsMode.HTTP2)]
    public class LoadWearableByParamSystemShould : LoadSystemBaseShould<LoadWearablesByParamSystem, WearablesResponse, GetWearableByParamIntention>
    {
        public LoadWearableByParamSystemShould(WebRequestsMode webRequestsMode) : base(webRequestsMode) { }

        private WearableStorage wearableStorage;

        private readonly string existingURN = "urn:decentraland:off-chain:base-avatars:aviatorstyle";

        private string successPath => $"file://{Application.dataPath}/../TestResources/Wearables/SuccessUserParam";

        private string failPath => $"file://{Application.dataPath}/../TestResources/Wearables/non_existing";
        private string wrongTypePath => $"file://{Application.dataPath + "/../TestResources/CRDT/arraybuffer.test"}";
        private int totalAmount => 0;

        protected override LoadWearablesByParamSystem CreateSystem(IWebRequestController webRequestController)
        {
            wearableStorage = new WearableStorage();

            IRealmData realmData = Substitute.For<IRealmData>();
            realmData.Configured.Returns(true);

            return new LoadWearablesByParamSystem(world, webRequestController, cache, realmData,
                URLSubdirectory.EMPTY, URLSubdirectory.FromString("Wearables"), wearableStorage);
        }

        protected override void AssertSuccess(WearablesResponse asset)
        {
            base.AssertSuccess(asset);

            foreach (string wearableCatalogKey in wearableStorage.wearablesCache.Keys)
                Debug.Log(wearableCatalogKey);

            Assert.AreEqual(wearableStorage.wearablesCache.Count, 1);
            Assert.NotNull(wearableStorage.wearablesCache[existingURN]);
        }

        [Test]
        public async Task ConcludeSuccessOnExistingWearable()
        {
            wearableStorage.wearablesCache.Add(existingURN, Substitute.For<IWearable>());
            await ConcludeSuccess();
        }

        protected override GetWearableByParamIntention CreateSuccessIntention() =>
            EmulateURL(successPath);

        protected override GetWearableByParamIntention CreateNotFoundIntention() =>
            EmulateURL(failPath);

        protected override GetWearableByParamIntention CreateWrongTypeIntention() =>
            EmulateURL(wrongTypePath);

        private GetWearableByParamIntention EmulateURL(string path)
        {
            IURLBuilder urlBuilder = Substitute.For<IURLBuilder>();
            urlBuilder.AppendDomainWithReplacedPath(Arg.Any<URLDomain>(), Arg.Any<URLSubdirectory>()).Returns(urlBuilder);
            urlBuilder.AppendSubDirectory(Arg.Any<URLSubdirectory>()).Returns(urlBuilder);
            urlBuilder.GetResult().Returns(path);
            urlBuilder.Build().Returns(new Uri(path));

            system!.urlBuilder = urlBuilder;

            return new GetWearableByParamIntention(Array.Empty<(string, string)>(), path, new List<IWearable>(), totalAmount);
        }
    }
}
