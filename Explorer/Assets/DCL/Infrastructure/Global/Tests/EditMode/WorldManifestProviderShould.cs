using CommunicationData.URLHelpers;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using ECS;
using Global.Dynamic;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;

namespace Global.Tests.EditMode
{
    public class WorldManifestProviderShould
    {
        private static readonly URLDomain ASSET_BUNDLE_REGISTRY = URLDomain.FromString("https://asset-bundle-registry.interconnected.online");

        /// <summary>
        ///     The genesis manifest is decentraland's own static artifact. A --base-domain deployment's realm can carry
        ///     one of Genesis City's realm names without being that city, so the fetch must not happen at all — issuing
        ///     it would describe the wrong world rather than merely waste a request.
        /// </summary>
        [TestCase("main")]
        [TestCase("baldr")]
        public async Task SkipTheGenesisFetchEntirelyForACustomDeployment(string realmName)
        {
            IWebRequestController webRequestController = Substitute.For<IWebRequestController>();
            var provider = new WorldManifestProvider(webRequestController);

            WorldManifest manifest = await provider.FetchWorldManifestAsync(ASSET_BUNDLE_REGISTRY, realmName, DecentralandEnvironment.Custom, CancellationToken.None);

            Assert.IsTrue(manifest.IsEmpty, "a custom deployment has no genesis manifest");
            CollectionAssert.IsEmpty(webRequestController.ReceivedCalls(), "no request may be issued for it");
        }

        /// <summary>
        ///     A realm that is neither a genesis realm nor a world has no manifest in any environment, so this pins the
        ///     no-request outcome as the shared baseline rather than something specific to a custom deployment.
        /// </summary>
        [Test]
        public async Task SkipTheFetchForARealmThatIsNeitherGenesisNorAWorld()
        {
            IWebRequestController webRequestController = Substitute.For<IWebRequestController>();
            var provider = new WorldManifestProvider(webRequestController);

            WorldManifest manifest = await provider.FetchWorldManifestAsync(ASSET_BUNDLE_REGISTRY, "some-private-catalyst", DecentralandEnvironment.Org, CancellationToken.None);

            Assert.IsTrue(manifest.IsEmpty);
            CollectionAssert.IsEmpty(webRequestController.ReceivedCalls());
        }
    }
}
