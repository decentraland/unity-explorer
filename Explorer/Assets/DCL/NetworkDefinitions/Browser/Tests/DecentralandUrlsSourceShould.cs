using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility;
using ECS;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace DCL.Browser.DecentralandUrls.Tests
{
    public class DecentralandUrlsSourceShould
    {
        private const string CUSTOM_DOMAIN = "interconnected.online";

        // The feature-flag singleton takes one Initialize per Reset; clear it around every test
        [SetUp]
        public void SetUp() => FeatureFlagsConfiguration.Reset();

        [TearDown]
        public void TearDown() => FeatureFlagsConfiguration.Reset();

        private static void InitializeFeatureFlags(bool optimizedAssets, string? customBaseUrl = null, bool useGateway = false, bool assetBundleFallback = false, bool abgenPipeline = false)
        {
            var dto = new FeatureFlagsResultDto
            {
                flags = new Dictionary<string, bool>
                {
                    [FeatureFlagsStrings.OPTIMIZED_ASSETS] = optimizedAssets,
                    [FeatureFlagsStrings.USE_GATEWAY] = useGateway,
                    [FeatureFlagsStrings.ASSET_BUNDLE_FALLBACK] = assetBundleFallback,
                    [FeatureFlagsStrings.ABGEN_PIPELINE] = abgenPipeline,
                },
                variants = new Dictionary<string, FeatureFlagVariantDto>(),
            };

            if (customBaseUrl != null)
                dto.variants[FeatureFlagsStrings.OPTIMIZED_ASSETS] = new FeatureFlagVariantDto
                {
                    name = FeatureFlagsStrings.OPTIMIZED_ASSETS_BASE_URL_VARIANT,
                    enabled = true,
                    payload = new FeatureFlagPayload { type = "string", value = customBaseUrl },
                };

            FeatureFlagsConfiguration.Reset();
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(dto));
        }

        [TestCase(DecentralandEnvironment.Org, "https://ab-cdn.decentraland.org", "https://lod-generator-unity-cdn.decentraland.org", "https://asset-bundle-registry.decentraland.org")]
        [TestCase(DecentralandEnvironment.Zone, "https://ab-cdn.decentraland.zone", "https://lod-generator-unity-cdn.decentraland.zone", "https://asset-bundle-registry.decentraland.zone")]
        public void UseDedicatedHostsWhenOptimizedAssetsDisabled(DecentralandEnvironment environment, string expectedAssetBundles, string expectedLods, string expectedRegistry)
        {
            InitializeFeatureFlags(optimizedAssets: false);
            DecentralandUrlsSource urlsSource = DecentralandUrlsSource.CreateForTest(environment, ILaunchMode.PLAY);

            Assert.AreEqual(expectedAssetBundles, urlsSource.Url(DecentralandUrl.AssetBundlesCDN));
            Assert.AreEqual(expectedLods, urlsSource.Url(DecentralandUrl.LodGeneratorCDN));
            Assert.AreEqual(expectedRegistry, urlsSource.Url(DecentralandUrl.AssetBundleRegistry));
            Assert.AreEqual($"{expectedRegistry}/entities/versions", urlsSource.Url(DecentralandUrl.AssetBundleRegistryVersion));
        }

        [TestCase(DecentralandEnvironment.Org, "https://abcdn.decentraland.org")]
        [TestCase(DecentralandEnvironment.Zone, "https://abcdn.decentraland.zone")]
        public void UseSingleDomainWhenOptimizedAssetsEnabled(DecentralandEnvironment environment, string expectedBaseUrl)
        {
            InitializeFeatureFlags(optimizedAssets: true);
            DecentralandUrlsSource urlsSource = DecentralandUrlsSource.CreateForTest(environment, ILaunchMode.PLAY);

            Assert.AreEqual(expectedBaseUrl, urlsSource.Url(DecentralandUrl.AssetBundlesCDN));
            Assert.AreEqual(expectedBaseUrl, urlsSource.Url(DecentralandUrl.LodGeneratorCDN));
            Assert.AreEqual(expectedBaseUrl, urlsSource.Url(DecentralandUrl.AssetBundleRegistry));
            Assert.AreEqual($"{expectedBaseUrl}/entities/versions", urlsSource.Url(DecentralandUrl.AssetBundleRegistryVersion));
        }

        [Test]
        public void ApplyOptimizedAssetsInLocalSceneDevelopmentMode()
        {
            InitializeFeatureFlags(optimizedAssets: true);
            DecentralandUrlsSource urlsSource = DecentralandUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.LOCAL_SCENE_DEVELOPMENT);

            Assert.AreEqual("https://abcdn.decentraland.org", urlsSource.Url(DecentralandUrl.AssetBundlesCDN));
            Assert.AreEqual("https://abcdn.decentraland.org", urlsSource.Url(DecentralandUrl.LodGeneratorCDN));
            Assert.AreEqual("https://abcdn.decentraland.org", urlsSource.Url(DecentralandUrl.AssetBundleRegistry));
        }

        [Test]
        public void ApplyCliOverrideForLocalPreviewSidecar()
        {
            // The flag variant is fleet-scoped, so a per-machine preview sidecar needs the CLI arg (forces on + overrides verbatim)
            InitializeFeatureFlags(optimizedAssets: false);
            var urlsSource = new DecentralandUrlsSource(DecentralandEnvironment.Org, new IRealmData.Fake(), ILaunchMode.LOCAL_SCENE_DEVELOPMENT, cliOptimizedAssetsUrl: "http://127.0.0.1:5147");

            Assert.AreEqual("http://127.0.0.1:5147", urlsSource.Url(DecentralandUrl.AssetBundlesCDN));
            Assert.AreEqual("http://127.0.0.1:5147", urlsSource.Url(DecentralandUrl.LodGeneratorCDN));
            Assert.AreEqual("http://127.0.0.1:5147", urlsSource.Url(DecentralandUrl.AssetBundleRegistry));
            Assert.AreEqual("http://127.0.0.1:5147/entities/versions", urlsSource.Url(DecentralandUrl.AssetBundleRegistryVersion));
        }

        [Test]
        public void ComposeRegistryEndpointsForWearablesAndWorldsOffTheUnifiedBase()
        {
            InitializeFeatureFlags(optimizedAssets: true, assetBundleFallback: true);
            DecentralandUrlsSource urlsSource = DecentralandUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY);

            Assert.AreEqual("https://abcdn.decentraland.org/entities/active", urlsSource.Url(DecentralandUrl.EntitiesActiveElements));

            // Realm-dependent, so Url() gates them on a configured realm; probe to resolve without one
            Assert.AreEqual("https://abcdn.decentraland.org/entities/active", urlsSource.Probe(DecentralandUrl.EntitiesActive));
            Assert.AreEqual("https://abcdn.decentraland.org/entities/active?world_name={0}", urlsSource.Probe(DecentralandUrl.WorldEntitiesActive));
        }

        [Test]
        public void PinProfilesToTheRegularRegistryRegardlessOfAssetFlags()
        {
            InitializeFeatureFlags(optimizedAssets: true, assetBundleFallback: true, abgenPipeline: true);
            DecentralandUrlsSource urlsSource = DecentralandUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY);

            Assert.AreEqual("https://asset-bundle-registry.decentraland.org/profiles", urlsSource.Url(DecentralandUrl.Profiles));
            Assert.AreEqual("https://asset-bundle-registry.decentraland.org/profiles/metadata", urlsSource.Url(DecentralandUrl.ProfilesMetadata));
        }

        [Test]
        public void UseCustomBaseUrlFromVariantPayload()
        {
            InitializeFeatureFlags(optimizedAssets: true, customBaseUrl: "https://assets.example.com");
            DecentralandUrlsSource urlsSource = DecentralandUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY);

            Assert.AreEqual("https://assets.example.com", urlsSource.Url(DecentralandUrl.AssetBundlesCDN));
            Assert.AreEqual("https://assets.example.com", urlsSource.Url(DecentralandUrl.LodGeneratorCDN));
            Assert.AreEqual("https://assets.example.com", urlsSource.Url(DecentralandUrl.AssetBundleRegistry));
        }

        [Test]
        public void UseArbitrarySchemeAndHostFromVariantPayload()
        {
            InitializeFeatureFlags(optimizedAssets: true, customBaseUrl: "http://ab.internal:5147");
            DecentralandUrlsSource urlsSource = DecentralandUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY);

            Assert.AreEqual("http://ab.internal:5147", urlsSource.Url(DecentralandUrl.AssetBundlesCDN));
            Assert.AreEqual("http://ab.internal:5147", urlsSource.Url(DecentralandUrl.LodGeneratorCDN));
            Assert.AreEqual("http://ab.internal:5147/entities/versions", urlsSource.Url(DecentralandUrl.AssetBundleRegistryVersion));
        }

        [Test]
        public void TrimTrailingSlashFromPayloadOverride()
        {
            InitializeFeatureFlags(optimizedAssets: true, customBaseUrl: "https://assets.example.com/");
            DecentralandUrlsSource urlsSource = DecentralandUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY);

            Assert.AreEqual("https://assets.example.com", urlsSource.Url(DecentralandUrl.AssetBundlesCDN));
            Assert.AreEqual("https://assets.example.com/entities/versions", urlsSource.Url(DecentralandUrl.AssetBundleRegistryVersion));
        }

        [TestCase(DecentralandEnvironment.Org, "https://abcdn.decentraland.org")]
        [TestCase(DecentralandEnvironment.Zone, "https://abcdn.decentraland.zone")]
        public void ApplyFlagArrivingAfterConstructionTimeProbing(DecentralandEnvironment environment, string expectedBaseUrl)
        {
            // LogEnvironment probes every url at construction, before a realm is entered, so realm-dependent urls resolve
            // to their <NOT_CONFIGURED> sentinel - an unconfigured realm models that
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
            var urlsSource = new DecentralandUrlsSource(environment, Substitute.For<IRealmData>(), ILaunchMode.PLAY);

            foreach (DecentralandUrl url in Enum.GetValues(typeof(DecentralandUrl)))
                urlsSource.Probe(url);

            InitializeFeatureFlags(optimizedAssets: true);

            Assert.AreEqual(expectedBaseUrl, urlsSource.Url(DecentralandUrl.AssetBundlesCDN));
            Assert.AreEqual(expectedBaseUrl, urlsSource.Url(DecentralandUrl.LodGeneratorCDN));
            Assert.AreEqual(expectedBaseUrl, urlsSource.Url(DecentralandUrl.AssetBundleRegistry));
            Assert.AreEqual($"{expectedBaseUrl}/entities/versions", urlsSource.Url(DecentralandUrl.AssetBundleRegistryVersion));
        }

        [Test]
        public void ApplyFlagArrivingAfterEarlyResolutionOfRegistryComposedUrls()
        {
            // An early consumer resolving registry-composed urls through Url() before the flag arrives must not pin the pre-flag base
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
            DecentralandUrlsSource urlsSource = DecentralandUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY);

            Assert.AreEqual("https://asset-bundle-registry.decentraland.org/entities/active", urlsSource.Url(DecentralandUrl.EntitiesActiveElements));

            InitializeFeatureFlags(optimizedAssets: true);

            Assert.AreEqual("https://abcdn.decentraland.org/entities/active", urlsSource.Url(DecentralandUrl.EntitiesActiveElements));
        }

        [Test]
        public void KeepTodayConstructionPinsWhenFlagsArriveLater()
        {
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
            DecentralandUrlsSource urlsSource = DecentralandUrlsSource.CreateForTest(DecentralandEnvironment.Today, ILaunchMode.PLAY);

            InitializeFeatureFlags(optimizedAssets: true);

            Assert.AreEqual("https://ab-cdn.decentraland.today", urlsSource.Url(DecentralandUrl.AssetBundlesCDN));
            Assert.AreEqual("https://asset-bundle-registry.decentraland.today", urlsSource.Url(DecentralandUrl.AssetBundleRegistry));
            Assert.AreEqual("https://asset-bundle-registry.decentraland.today/profiles", urlsSource.Url(DecentralandUrl.Profiles));
        }

        [TestCase(DecentralandEnvironment.Org)]
        [TestCase(DecentralandEnvironment.Zone)]
        public void FlipCdnAndRegistryTogetherWhenAbgenPipelineEnabled(DecentralandEnvironment environment)
        {
            InitializeFeatureFlags(optimizedAssets: false, abgenPipeline: true);
            DecentralandUrlsSource urlsSource = DecentralandUrlsSource.CreateForTest(environment, ILaunchMode.PLAY);
            string env = environment.ToString().ToLower();

            Assert.AreEqual($"https://abgen-cdn.decentraland.{env}", urlsSource.Url(DecentralandUrl.AssetBundlesCDN));
            Assert.AreEqual($"https://asset-bundle-registry-abgen.decentraland.{env}", urlsSource.Url(DecentralandUrl.AssetBundleRegistry));
            Assert.AreEqual($"https://asset-bundle-registry-abgen.decentraland.{env}/entities/versions", urlsSource.Url(DecentralandUrl.AssetBundleRegistryVersion));

            // LOD bundles are only produced by the regular pipeline
            Assert.AreEqual($"https://ab-cdn.decentraland.{env}", urlsSource.Url(DecentralandUrl.LodAssetBundlesCDN));
        }

        [Test]
        public void ForceTheAbgenPipelineOnWithTheLaunchArg()
        {
            InitializeFeatureFlags(optimizedAssets: false, abgenPipeline: false);
            var forcedOn = new DecentralandUrlsSource(DecentralandEnvironment.Org, new IRealmData.Fake(), ILaunchMode.PLAY, abgenPipelineForced: true);

            Assert.AreEqual("https://abgen-cdn.decentraland.org", forcedOn.Url(DecentralandUrl.AssetBundlesCDN));
            Assert.AreEqual("https://asset-bundle-registry-abgen.decentraland.org", forcedOn.Url(DecentralandUrl.AssetBundleRegistry));
        }

        [Test]
        public void KeepAbgenHostsOffTheGatewayButLodsAndProfilesOnIt()
        {
            InitializeFeatureFlags(optimizedAssets: false, useGateway: true, abgenPipeline: true);
            GatewayUrlsSource urlsSource = GatewayUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY);

            Assert.AreEqual("https://abgen-cdn.decentraland.org", urlsSource.Url(DecentralandUrl.AssetBundlesCDN));
            Assert.AreEqual("https://asset-bundle-registry-abgen.decentraland.org", urlsSource.Url(DecentralandUrl.AssetBundleRegistry));

            Assert.AreEqual("https://gateway.decentraland.org/ab-cdn", urlsSource.Url(DecentralandUrl.LodAssetBundlesCDN));
            Assert.AreEqual("https://gateway.decentraland.org/asset-bundle-registry/profiles", urlsSource.Url(DecentralandUrl.Profiles));
        }

        [Test]
        public void KeepOptimizedAssetsOutOfTheGateway()
        {
            InitializeFeatureFlags(optimizedAssets: true, useGateway: true);
            GatewayUrlsSource urlsSource = GatewayUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY);

            Assert.AreEqual("https://abcdn.decentraland.org", urlsSource.Url(DecentralandUrl.AssetBundlesCDN));
            Assert.AreEqual("https://abcdn.decentraland.org", urlsSource.Url(DecentralandUrl.AssetBundleRegistry));
            Assert.AreEqual("https://abcdn.decentraland.org/entities/versions", urlsSource.Url(DecentralandUrl.AssetBundleRegistryVersion));
            Assert.AreEqual("https://gateway.decentraland.org/auth-api", urlsSource.Url(DecentralandUrl.ApiAuth));
        }

        // Naming a gateway forces routing on, so the flag has no say either way.
        [TestCase(true, "https://gateway.localhost/", "https://gateway.localhost/places/api/places")]
        [TestCase(false, "https://gateway.localhost/", "https://gateway.localhost/places/api/places")]
        [TestCase(false, "https://edge.localhost/gw/", "https://edge.localhost/gw/places/api/places")]
        public void RouteThroughTheGatewayOriginTheArgNames(bool useGateway, string gatewayPrefix, string expected)
        {
            InitializeFeatureFlags(optimizedAssets: false, useGateway: useGateway);
            var urlsSource = new GatewayUrlsSource(DecentralandEnvironment.Org, new IRealmData.Fake(), ILaunchMode.PLAY, cliGatewayPrefix: gatewayPrefix);

            Assert.AreEqual(expected, urlsSource.Url(DecentralandUrl.ApiPlaces));
        }

        [TestCase("https://gateway.localhost", "https://gateway.localhost/")]
        [TestCase("https://gateway.localhost/", "https://gateway.localhost/")]
        [TestCase("  https://gateway.localhost  ", "https://gateway.localhost/")]
        [TestCase("http://127.0.0.1:8080", "http://127.0.0.1:8080/")]
        [TestCase("https://edge.localhost/gw", "https://edge.localhost/gw/")]
        public void NormalizeAGatewayOrigin(string gatewayUrl, string expected)
        {
            Assert.IsTrue(GatewayUrlsSource.TryNormalizeGatewayPrefix(gatewayUrl, out string prefix), gatewayUrl);
            Assert.AreEqual(expected, prefix);
        }

        // A mistyped gateway would send every supported service somewhere unintended, so MainSceneLoader reports and
        // ends the launch instead of coercing it into something plausible.
        [TestCase("not-a-url")]
        [TestCase("gateway.localhost")]
        [TestCase("ftp://gateway.localhost")]
        [TestCase("https://gateway.localhost?x=1")]
        [TestCase("https://gateway.localhost#fragment")]
        public void RejectAGatewayUrlThatIsNotAnOrigin(string gatewayUrl)
        {
            Assert.IsFalse(GatewayUrlsSource.TryNormalizeGatewayPrefix(gatewayUrl, out _), gatewayUrl);
        }

        // Signed fetch signs the un-gatewayed url, so the custom base has to reverse as cleanly as the default one.
        [Test]
        public void ReverseTheGatewayBaseForSignedFetch()
        {
            InitializeFeatureFlags(optimizedAssets: false, useGateway: false);
            var urlsSource = new GatewayUrlsSource(DecentralandEnvironment.Org, new IRealmData.Fake(), ILaunchMode.PLAY, cliGatewayPrefix: "https://gateway.localhost/");

            Assert.AreEqual("https://places.decentraland.org/api/places", urlsSource.GetOriginalUrl(urlsSource.Url(DecentralandUrl.ApiPlaces)));
        }

        // The arg replaces the origin and outranks the flag, never the environment: today is not gateway-routed.
        [Test]
        public void KeepTodayOffTheGatewayEvenWithAGatewayOrigin()
        {
            InitializeFeatureFlags(optimizedAssets: false, useGateway: false);
            var urlsSource = new GatewayUrlsSource(DecentralandEnvironment.Today, new IRealmData.Fake(), ILaunchMode.PLAY, cliGatewayPrefix: "https://gateway.localhost/");

            Assert.AreEqual("https://places.decentraland.org/api/places", urlsSource.Url(DecentralandUrl.ApiPlaces));
        }

        [Test]
        public void KeepGatewayRoutingWhenOptimizedAssetsDisabled()
        {
            InitializeFeatureFlags(optimizedAssets: false, useGateway: true);
            GatewayUrlsSource urlsSource = GatewayUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY);

            Assert.AreEqual("https://gateway.decentraland.org/ab-cdn", urlsSource.Url(DecentralandUrl.AssetBundlesCDN));
            Assert.AreEqual("https://gateway.decentraland.org/asset-bundle-registry", urlsSource.Url(DecentralandUrl.AssetBundleRegistry));
            Assert.AreEqual("https://gateway.decentraland.org/asset-bundle-registry/entities/versions", urlsSource.Url(DecentralandUrl.AssetBundleRegistryVersion));
        }

        [Test]
        public void KeepNonDecentralandHostsOffTheGateway()
        {
            InitializeFeatureFlags(optimizedAssets: false, useGateway: true);
            var urlsSource = new GatewayUrlsSource(DecentralandEnvironment.Org, new IRealmData.Fake(), ILaunchMode.PLAY, cliGatekeeperUrl: "https://gatekeeper.example.com");

            Assert.AreEqual("https://gatekeeper.example.com/get-scene-adapter", urlsSource.Url(DecentralandUrl.GateKeeperSceneAdapter));

            GatewayUrlsSource defaultSource = GatewayUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY);
            Assert.AreEqual("https://gateway.decentraland.org/comms-gatekeeper/get-scene-adapter", defaultSource.Url(DecentralandUrl.GateKeeperSceneAdapter));
        }

        [TestCase("https://gk.decentraland.org:8443")]
        [TestCase("https://sub.decentraland.org@evil.example.com")]
        [TestCase("https://x.decentraland.evil.example.com")]
        [TestCase("https://gk.decentraland.org.")]
        public void KeepHostsOutsideTheBaseDomainShapeOffTheGateway(string customHost)
        {
            InitializeFeatureFlags(optimizedAssets: false, useGateway: true);
            var urlsSource = new GatewayUrlsSource(DecentralandEnvironment.Org, new IRealmData.Fake(), ILaunchMode.PLAY, cliGatekeeperUrl: customHost);

            Assert.AreEqual($"{customHost}/get-scene-adapter", urlsSource.Url(DecentralandUrl.GateKeeperSceneAdapter));
        }

        [TestCase(DecentralandEnvironment.Org, IDecentralandUrlsSource.ORG_DOMAIN)]
        [TestCase(DecentralandEnvironment.Zone, IDecentralandUrlsSource.ZONE_DOMAIN)]
        [TestCase(DecentralandEnvironment.Today, IDecentralandUrlsSource.TODAY_DOMAIN)]
        public void SelectTheEnvironmentsOwnDomain(DecentralandEnvironment environment, string expectedBaseDomain)
        {
            Assert.AreEqual(expectedBaseDomain, DecentralandUrlsSource.ResolveBaseDomain(environment, null));
        }

        /// <summary>
        ///     What a constructed source reports is the domain it resolves urls against, which is not always the
        ///     domain the environment selects: today pins the handful of hosts it serves from .today while it is being
        ///     built and serves everything afterwards from org, so org is what it settles on.
        /// </summary>
        [TestCase(DecentralandEnvironment.Org, IDecentralandUrlsSource.ORG_DOMAIN)]
        [TestCase(DecentralandEnvironment.Zone, IDecentralandUrlsSource.ZONE_DOMAIN)]
        [TestCase(DecentralandEnvironment.Today, IDecentralandUrlsSource.ORG_DOMAIN)]
        public void ReportTheDomainUrlsResolveAgainst(DecentralandEnvironment environment, string expectedBaseDomain)
        {
            InitializeFeatureFlags(optimizedAssets: false);
            Assert.AreEqual(expectedBaseDomain, DecentralandUrlsSource.CreateForTest(environment, ILaunchMode.PLAY).BaseDomain);
        }

        [TestCase("worlds-content-server." + CUSTOM_DOMAIN, true)]
        [TestCase("a.b." + CUSTOM_DOMAIN, true)]                  // nested subdomains are still below it
        [TestCase(CUSTOM_DOMAIN, false)]                          // the domain itself is not a subdomain of itself
        [TestCase(CUSTOM_DOMAIN + ".attacker.com", false)]        // suffix-spoof
        [TestCase("evil-" + CUSTOM_DOMAIN, false)]                // no '.' boundary
        [TestCase("online", false)]                               // shorter than the domain
        public void MatchOnlySubdomainsOfADomain(string host, bool expected)
        {
            Assert.AreEqual(expected, IDecentralandUrlsSource.IsSubdomainOf(host, CUSTOM_DOMAIN), host);
        }

        [TestCase("worlds-content-server." + CUSTOM_DOMAIN, true)]
        [TestCase(CUSTOM_DOMAIN, true)]                           // the domain is a host in its own right here
        [TestCase("WORLDS-CONTENT-SERVER.INTERCONNECTED.ONLINE", true)]
        [TestCase(CUSTOM_DOMAIN + ".attacker.com", false)]
        [TestCase("evil-" + CUSTOM_DOMAIN, false)]
        public void MatchAnyHostWithinADomain(string host, bool expected)
        {
            Assert.AreEqual(expected, IDecentralandUrlsSource.IsHostWithinDomain(host, CUSTOM_DOMAIN), host);
        }

        [TestCase(CUSTOM_DOMAIN, CUSTOM_DOMAIN)]
        [TestCase("  " + CUSTOM_DOMAIN + "  ", CUSTOM_DOMAIN)] // padded by a shell or launcher
        [TestCase("." + CUSTOM_DOMAIN, CUSTOM_DOMAIN)]         // written as a suffix
        public void TakeBaseDomainFromTheCustomEnvironment(string customBaseDomain, string expectedBaseDomain)
        {
            InitializeFeatureFlags(optimizedAssets: false);
            Assert.AreEqual(expectedBaseDomain, DecentralandUrlsSource.CreateForTest(customBaseDomain, ILaunchMode.PLAY).BaseDomain);
        }

        [TestCase(DecentralandEnvironment.Custom, null)]                            // Custom has no domain of its own
        [TestCase(DecentralandEnvironment.Custom, "   ")]
        [TestCase(DecentralandEnvironment.Custom, "https://" + CUSTOM_DOMAIN)]      // a url, not a domain
        [TestCase(DecentralandEnvironment.Custom, CUSTOM_DOMAIN + "/path")]
        [TestCase(DecentralandEnvironment.Custom, CUSTOM_DOMAIN + ":8443")]
        [TestCase(DecentralandEnvironment.Custom, "evil.example@" + CUSTOM_DOMAIN)] // userinfo smuggled into the domain
        [TestCase(DecentralandEnvironment.Org, CUSTOM_DOMAIN)]                      // a base domain the environment would ignore
        [TestCase(DecentralandEnvironment.Zone, CUSTOM_DOMAIN)]
        public void RejectAMisconfiguredBaseDomain(DecentralandEnvironment environment, string? customBaseDomain)
        {
            Assert.Throws<ArgumentException>(() => DecentralandUrlsSource.ResolveBaseDomain(environment, customBaseDomain));
        }

        [TestCase(DecentralandUrl.Host, "https://" + CUSTOM_DOMAIN)]
        [TestCase(DecentralandUrl.PeerAbout, "https://peer." + CUSTOM_DOMAIN + "/about")]
        [TestCase(DecentralandUrl.PeerContent, "https://peer." + CUSTOM_DOMAIN + "/content/contents")]
        [TestCase(DecentralandUrl.Servers, "https://peer." + CUSTOM_DOMAIN + "/lambdas/contracts/servers")]
        [TestCase(DecentralandUrl.FeatureFlags, "https://feature-flags." + CUSTOM_DOMAIN)]
        [TestCase(DecentralandUrl.Gatekeeper, "https://comms-gatekeeper." + CUSTOM_DOMAIN)]
        [TestCase(DecentralandUrl.GateKeeperSceneAdapter, "https://comms-gatekeeper." + CUSTOM_DOMAIN + "/get-scene-adapter")]
        [TestCase(DecentralandUrl.LocalGateKeeperSceneAdapter, "https://comms-gatekeeper-local." + CUSTOM_DOMAIN + "/get-scene-adapter")]
        [TestCase(DecentralandUrl.Genesis, "https://realm-provider-ea." + CUSTOM_DOMAIN + "/main")]
        [TestCase(DecentralandUrl.WorldServer, "https://worlds-content-server." + CUSTOM_DOMAIN + "/world")]
        [TestCase(DecentralandUrl.Pulse, "pulse-server." + CUSTOM_DOMAIN)]
        [TestCase(DecentralandUrl.ApiRpc, "wss://rpc." + CUSTOM_DOMAIN)]
        [TestCase(DecentralandUrl.ChainRpc, "https://rpc." + CUSTOM_DOMAIN)]
        public void MoveEveryHostOntoTheCustomBaseDomain(DecentralandUrl url, string expected)
        {
            InitializeFeatureFlags(optimizedAssets: false);
            DecentralandUrlsSource urlsSource = DecentralandUrlsSource.CreateForTest(CUSTOM_DOMAIN, ILaunchMode.PLAY);

            Assert.AreEqual(expected, urlsSource.Url(url));
        }

        /// <summary>
        ///     The whole point of the base-domain seam: nothing may still resolve to a decentraland host, and no
        ///     template may leak its unsubstituted token. A new url added with a hand-written domain fails here.
        /// </summary>
        [Test]
        public void LeaveNoDecentralandHostBehindOnACustomBaseDomain()
        {
            InitializeFeatureFlags(optimizedAssets: false);
            var urlsSource = new DecentralandUrlsSource(DecentralandEnvironment.Custom, Substitute.For<IRealmData>(), ILaunchMode.PLAY, customBaseDomain: CUSTOM_DOMAIN);

            foreach (DecentralandUrl url in Enum.GetValues(typeof(DecentralandUrl)))
            {
                string resolved = urlsSource.Probe(url);

                // Off-platform links that are decentraland's own marketing surface, not a backend host a custom
                // deployment could serve.
                if (url == DecentralandUrl.DecentralandWorlds)
                    continue;

                foreach (string domain in IDecentralandUrlsSource.ALL_DOMAINS)
                    Assert.IsTrue(resolved.IndexOf(domain, StringComparison.OrdinalIgnoreCase) < 0, $"{url} still resolves to {domain}: {resolved}");
            }
        }

        [TestCase(DecentralandEnvironment.Org, null, "https://feature-flags." + IDecentralandUrlsSource.ORG_DOMAIN)]
        [TestCase(DecentralandEnvironment.Zone, null, "https://feature-flags." + IDecentralandUrlsSource.ZONE_DOMAIN)]
        [TestCase(DecentralandEnvironment.Custom, CUSTOM_DOMAIN, "https://feature-flags." + CUSTOM_DOMAIN)]
        public void ResolveThePreLoginFeatureFlagsHost(DecentralandEnvironment environment, string? customBaseDomain, string expected)
        {
            Assert.AreEqual(expected, DecentralandUrlsSource.GetFeatureFlagsUrl(environment, customBaseDomain));
        }

        [Test]
        public void RouteACustomBaseDomainThroughItsOwnGateway()
        {
            InitializeFeatureFlags(optimizedAssets: false, useGateway: true);
            GatewayUrlsSource urlsSource = GatewayUrlsSource.CreateForTest(CUSTOM_DOMAIN, ILaunchMode.PLAY);

            Assert.AreEqual("https://gateway." + CUSTOM_DOMAIN + "/auth-api", urlsSource.Url(DecentralandUrl.ApiAuth));
            Assert.AreEqual("https://gateway." + CUSTOM_DOMAIN + "/comms-gatekeeper/get-scene-adapter", urlsSource.Url(DecentralandUrl.GateKeeperSceneAdapter));
            Assert.AreEqual("https://gateway." + CUSTOM_DOMAIN + "/comms-gatekeeper/private-messages/token", urlsSource.Url(DecentralandUrl.ChatAdapter));

            // Its host is composed from the base domain like any other, so it routes through the gateway here
            // exactly as it does on org.
            Assert.AreEqual("https://gateway." + CUSTOM_DOMAIN + "/comms-gatekeeper-local/get-scene-adapter", urlsSource.Url(DecentralandUrl.LocalGateKeeperSceneAdapter));
        }

        [Test]
        public void KeepTheGatekeeperOverrideAboveTheCustomBaseDomain()
        {
            InitializeFeatureFlags(optimizedAssets: false, useGateway: true);
            var urlsSource = new GatewayUrlsSource(DecentralandEnvironment.Custom, new IRealmData.Fake(), ILaunchMode.PLAY, cliGatekeeperUrl: "https://gk.example.com", customBaseDomain: CUSTOM_DOMAIN);

            Assert.AreEqual("https://gk.example.com/get-scene-adapter", urlsSource.Url(DecentralandUrl.GateKeeperSceneAdapter));
            Assert.AreEqual("https://gk.example.com/get-scene-adapter", urlsSource.Url(DecentralandUrl.LocalGateKeeperSceneAdapter));
        }
    }
}
