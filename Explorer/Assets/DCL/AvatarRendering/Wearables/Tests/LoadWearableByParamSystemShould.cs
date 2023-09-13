using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Systems;
using ECS;
using ECS.StreamableLoading.Tests;
using Ipfs;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Utility.Multithreading;
using ParamPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.Wearable[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearableByParamIntention>;

namespace DCL.AvatarRendering.Wearables.Tests
{
    [TestFixture]
    public class LoadWearableByParamSystemShould : LoadSystemBaseShould<LoadWearablesByParamSystem, IWearable[], GetWearableByParamIntention>
    {
        private Dictionary<string, IWearable> wearableCatalog;
        private readonly string existingURN = "urn:decentraland:off-chain:base-avatars:aviatorstyle";
        private readonly string successURL = $"file://{Application.dataPath}/../TestResources/Wearables/SuccessUserParam";
        private string failPath => $"file://{Application.dataPath}/../TestResources/Wearables/non_existing";
        private string wrongTypePath => $"file://{Application.dataPath + "/../TestResources/CRDT/arraybuffer.test"}";

        protected override LoadWearablesByParamSystem CreateSystem()
        {
            wearableCatalog = new Dictionary<string, IWearable>();

            return new LoadWearablesByParamSystem(world, cache, new RealmData(new IpfsRealm(URLDomain.EMPTY)),
                URLSubdirectory.EMPTY, URLSubdirectory.EMPTY, wearableCatalog, new MutexSync());
        }

        protected override void AssertSuccess(IWearable[] asset)
        {
            base.AssertSuccess(asset);

            foreach (string wearableCatalogKey in wearableCatalog.Keys)
                Debug.Log(wearableCatalogKey);

            Assert.AreEqual(wearableCatalog.Count, 1);
            Assert.NotNull(wearableCatalog[existingURN]);
        }

        [Test]
        public async Task ConcludeSuccessOnExistingWearable()
        {
            wearableCatalog.Add(existingURN, Substitute.For<IWearable>());
            await ConcludeSuccess();
        }

        protected override GetWearableByParamIntention CreateSuccessIntention() =>
            new (Array.Empty<(string, string)>(), successURL, new List<IWearable>());

        protected override GetWearableByParamIntention CreateNotFoundIntention() =>
            new (Array.Empty<(string, string)>(), failPath, new List<IWearable>());

        protected override GetWearableByParamIntention CreateWrongTypeIntention() =>
            new (Array.Empty<(string, string)>(), wrongTypePath, new List<IWearable>());
    }
}
