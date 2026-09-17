using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using ECS;
using Global.Dynamic;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;

namespace Global.Tests.EditMode
{
    public class WorldManifestProviderShould
    {
        private const string CUSTOM_MANIFEST_URL = "https://peer.interconnected.online/world-manifest.json";
        private const string MANIFEST_JSON = "{\"occupied\":[\"0,0\",\"1,0\"],\"spawn_coordinate\":{\"x\":1,\"y\":0},\"total\":2}";
        private static readonly URLDomain ASSET_BUNDLE_REGISTRY = URLDomain.FromString("https://asset-bundle-registry.interconnected.online");

        private readonly List<string> requestedUrls = new ();
        private IWebRequestController webRequestController = null!;
        private WorldManifestProvider provider = null!;

        [SetUp]
        public void SetUp()
        {
            requestedUrls.Clear();
            webRequestController = Substitute.For<IWebRequestController>();
            IDecentralandUrlsSource urlsSource = Substitute.For<IDecentralandUrlsSource>();
            urlsSource.Url(DecentralandUrl.GenesisWorldManifest).Returns(CUSTOM_MANIFEST_URL);
            provider = new WorldManifestProvider(webRequestController, urlsSource);
            RespondWith(UniTask.FromResult<string?>(string.Empty));
        }

        [TestCase("dcl-one")]
        [TestCase("main")]
        public async Task LoadAndCacheTheDeploymentManifestForCustomGenesis(string realmName)
        {
            RespondWith(UniTask.FromResult<string?>(MANIFEST_JSON));

            WorldManifest manifest = await provider.FetchWorldManifestAsync(ASSET_BUNDLE_REGISTRY, realmName, DecentralandEnvironment.Custom, true, CancellationToken.None);
            try
            {
                Assert.IsFalse(manifest.IsEmpty);
                Assert.AreEqual(2, manifest.GetOccupiedParcels().Count);
                Assert.IsTrue(manifest.GetOccupiedParcels().Contains(new int2(1, 0)));
                Assert.AreEqual(1, manifest.spawn_coordinate.x);
                Assert.IsTrue(manifest.IsParcelInsideBoundaries(1, 0));

                WorldManifest cached = await provider.FetchWorldManifestAsync(ASSET_BUNDLE_REGISTRY, realmName, DecentralandEnvironment.Custom, true, CancellationToken.None);
                Assert.IsFalse(cached.IsEmpty);
                CollectionAssert.AreEqual(new[] { CUSTOM_MANIFEST_URL }, requestedUrls);
            }
            finally
            {
                if (!manifest.IsEmpty)
                    manifest.GetOccupiedParcels().Dispose();
            }
        }

        [TestCase("")]
        [TestCase("{\"occupied\":[]}")]
        [TestCase("invalid json")]
        public async Task ReturnEmptyForAnEmptyOrInvalidManifest(string json)
        {
            RespondWith(UniTask.FromResult<string?>(json));

            WorldManifest manifest = await provider.FetchWorldManifestAsync(ASSET_BUNDLE_REGISTRY, "dcl-one", DecentralandEnvironment.Custom, true, CancellationToken.None);

            Assert.IsTrue(manifest.IsEmpty);
            CollectionAssert.AreEqual(new[] { CUSTOM_MANIFEST_URL }, requestedUrls);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ReturnEmptyWhenTheRequestFailsOrIsCancelled(bool cancelled)
        {
            RespondWith(cancelled
                ? UniTask.FromCanceled<string?>(new CancellationToken(true))
                : UniTask.FromException<string?>(new InvalidOperationException("Manifest unavailable")));

            WorldManifest manifest = await provider.FetchWorldManifestAsync(ASSET_BUNDLE_REGISTRY, "dcl-one", DecentralandEnvironment.Custom, true, CancellationToken.None);

            Assert.IsTrue(manifest.IsEmpty);
            CollectionAssert.AreEqual(new[] { CUSTOM_MANIFEST_URL }, requestedUrls);
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public async Task SkipGenesisFetchForFixedScenesAndLocalSceneDevelopment(bool localScene, bool fixedScenes)
        {
            IIpfsRealm realm = Substitute.For<IIpfsRealm>();
            realm.SceneUrns.Returns(fixedScenes ? new[] { "urn:decentraland:entity:test" } : Array.Empty<string>());
            bool isGenesis = RealmData.ClassifyRealm(localScene, realm) == RealmKind.GenesisCity;

            WorldManifest manifest = await provider.FetchWorldManifestAsync(ASSET_BUNDLE_REGISTRY, "main", DecentralandEnvironment.Custom, isGenesis, CancellationToken.None);

            Assert.IsFalse(isGenesis);
            Assert.IsTrue(manifest.IsEmpty);
            Assert.IsEmpty(requestedUrls);
        }

        [TestCase(DecentralandEnvironment.Org, "https://places-dcf8abb.s3.amazonaws.com/WorldManifest.json")]
        [TestCase(DecentralandEnvironment.Zone, "https://places-e22845c.s3.us-east-1.amazonaws.com/WorldManifest.json")]
        public async Task KeepCanonicalGenesisManifestUrls(DecentralandEnvironment environment, string expectedUrl)
        {
            await provider.FetchWorldManifestAsync(ASSET_BUNDLE_REGISTRY, "main", environment, true, CancellationToken.None);

            CollectionAssert.AreEqual(new[] { expectedUrl }, requestedUrls);
        }

        [Test]
        public async Task KeepWorldManifestRouting()
        {
            await provider.FetchWorldManifestAsync(ASSET_BUNDLE_REGISTRY, "example.dcl.eth", DecentralandEnvironment.Custom, false, CancellationToken.None);

            CollectionAssert.AreEqual(new[] { $"{ASSET_BUNDLE_REGISTRY.Value}/worlds/example.dcl.eth/manifest" }, requestedUrls);
        }

        [Test]
        public async Task SkipTheFetchForANonCanonicalRealmOnOrg()
        {
            WorldManifest manifest = await provider.FetchWorldManifestAsync(ASSET_BUNDLE_REGISTRY, "some-private-catalyst", DecentralandEnvironment.Org, true, CancellationToken.None);

            Assert.IsTrue(manifest.IsEmpty);
            Assert.IsEmpty(requestedUrls);
        }

        private void RespondWith(UniTask<string?> response)
        {
            webRequestController.SendAsync<GenericGetRequest, GenericGetArguments, GenericDownloadHandlerUtils.StoreTextOp<GenericGetRequest>, string>(
                Arg.Any<RequestEnvelope<GenericGetRequest, GenericGetArguments>>(),
                Arg.Any<GenericDownloadHandlerUtils.StoreTextOp<GenericGetRequest>>(),
                Arg.Any<long>(),
                Arg.Any<IProgress<float>?>()).Returns(call =>
            {
                requestedUrls.Add(call.Arg<RequestEnvelope<GenericGetRequest, GenericGetArguments>>().CommonArguments.URL.Value);
                return response;
            });
            requestedUrls.Clear();
        }
    }
}
