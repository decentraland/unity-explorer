using CommunicationData.URLHelpers;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using ECS;
using Global.Dynamic;
using NSubstitute;
using NUnit.Framework;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Global.Tests.EditMode
{
    public class WorldManifestProviderShould
    {
        private static readonly URLDomain ASSET_BUNDLE_REGISTRY = URLDomain.FromString("https://asset-bundle-registry.interconnected.online");
        private const string CUSTOM_MANIFEST_URL = "https://peer.interconnected.online/world-manifest.json";

        private static (WorldManifestProvider provider, IWebRequestController webRequestController) CreateProvider()
        {
            IWebRequestController webRequestController = Substitute.For<IWebRequestController>();
            IDecentralandUrlsSource urlsSource = Substitute.For<IDecentralandUrlsSource>();
            urlsSource.Url(DecentralandUrl.GenesisWorldManifest).Returns(CUSTOM_MANIFEST_URL);
            return (new WorldManifestProvider(webRequestController, urlsSource), webRequestController);
        }

        /// <summary>
        ///     A custom deployment's genesis realm is recognized by classification, not by decentraland's realm-name
        ///     list ("dcl-one" is on no list), and its manifest resolves from the deployment's own base domain — never
        ///     decentraland's S3 artifact, which describes a different city.
        /// </summary>
        [TestCase("dcl-one")]
        [TestCase("main")]
        public async Task FetchTheDeploymentOwnManifestForACustomGenesisRealm(string realmName)
        {
            (WorldManifestProvider provider, IWebRequestController webRequestController) = CreateProvider();

            WorldManifest manifest = await provider.FetchWorldManifestAsync(ASSET_BUNDLE_REGISTRY, realmName, DecentralandEnvironment.Custom, realmIsGenesis: true, CancellationToken.None);

            CommonArguments requested = webRequestController.ReceivedCalls()
                                                            .SelectMany(call => call.GetArguments())
                                                            .OfType<CommonArguments>()
                                                            .Single();

            Assert.AreEqual(CUSTOM_MANIFEST_URL, requested.URL.Value, "the manifest must come from the deployment's base domain");
            Assert.IsTrue(manifest.IsEmpty, "an unreachable manifest fails open to the empty manifest");
        }

        /// <summary>
        ///     A custom realm with fixed scene urns is not a genesis city even when it reuses a Genesis City realm
        ///     name, so no genesis fetch may be issued — applying any genesis manifest would describe the wrong world.
        /// </summary>
        [TestCase("main")]
        [TestCase("baldr")]
        public async Task SkipTheGenesisFetchForACustomRealmThatIsNotGenesisClassified(string realmName)
        {
            (WorldManifestProvider provider, IWebRequestController webRequestController) = CreateProvider();

            WorldManifest manifest = await provider.FetchWorldManifestAsync(ASSET_BUNDLE_REGISTRY, realmName, DecentralandEnvironment.Custom, realmIsGenesis: false, CancellationToken.None);

            Assert.IsTrue(manifest.IsEmpty, "a non-genesis custom realm has no genesis manifest");
            CollectionAssert.IsEmpty(webRequestController.ReceivedCalls(), "no request may be issued for it");
        }

        /// <summary>
        ///     The decentraland environments keep the realm-name gate: a private catalyst on Org is genesis-classified
        ///     (volatile scenes) yet still gets no manifest, pinning that the classification relaxation is scoped to
        ///     the Custom environment only.
        /// </summary>
        [Test]
        public async Task SkipTheFetchForARealmThatIsNeitherGenesisNorAWorld()
        {
            (WorldManifestProvider provider, IWebRequestController webRequestController) = CreateProvider();

            WorldManifest manifest = await provider.FetchWorldManifestAsync(ASSET_BUNDLE_REGISTRY, "some-private-catalyst", DecentralandEnvironment.Org, realmIsGenesis: true, CancellationToken.None);

            Assert.IsTrue(manifest.IsEmpty);
            CollectionAssert.IsEmpty(webRequestController.ReceivedCalls());
        }
    }
}
