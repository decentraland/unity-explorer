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
        // The feature-flag singleton takes one Initialize per Reset; clear it around every test
        [SetUp]
        public void SetUp() => FeatureFlagsConfiguration.Reset();

        [TearDown]
        public void TearDown() => FeatureFlagsConfiguration.Reset();

        private static void InitializeFeatureFlags(bool optimizedAssets, string? customBaseUrl = null, bool useGateway = false, bool assetBundleFallback = false)
        {
            var dto = new FeatureFlagsResultDto
            {
                flags = new Dictionary<string, bool>
                {
                    [FeatureFlagsStrings.OPTIMIZED_ASSETS] = optimizedAssets,
                    [FeatureFlagsStrings.USE_GATEWAY] = useGateway,
                    [FeatureFlagsStrings.ASSET_BUNDLE_FALLBACK] = assetBundleFallback,
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
        public void ComposeRegistryEndpointsForWearablesWorldsAndProfilesOffTheUnifiedBase()
        {
            InitializeFeatureFlags(optimizedAssets: true, assetBundleFallback: true);
            DecentralandUrlsSource urlsSource = DecentralandUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY);

            Assert.AreEqual("https://abcdn.decentraland.org/entities/active", urlsSource.Url(DecentralandUrl.EntitiesActiveElements));
            Assert.AreEqual("https://abcdn.decentraland.org/profiles", urlsSource.Url(DecentralandUrl.Profiles));
            Assert.AreEqual("https://abcdn.decentraland.org/profiles/metadata", urlsSource.Url(DecentralandUrl.ProfilesMetadata));

            // Realm-dependent, so Url() gates them on a configured realm; probe to resolve without one
            Assert.AreEqual("https://abcdn.decentraland.org/entities/active", urlsSource.Probe(DecentralandUrl.EntitiesActive));
            Assert.AreEqual("https://abcdn.decentraland.org/entities/active?world_name={0}", urlsSource.Probe(DecentralandUrl.WorldEntitiesActive));
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

            Assert.AreEqual("https://asset-bundle-registry.decentraland.org/profiles", urlsSource.Url(DecentralandUrl.Profiles));
            Assert.AreEqual("https://asset-bundle-registry.decentraland.org/entities/active", urlsSource.Url(DecentralandUrl.EntitiesActiveElements));

            InitializeFeatureFlags(optimizedAssets: true);

            Assert.AreEqual("https://abcdn.decentraland.org/profiles", urlsSource.Url(DecentralandUrl.Profiles));
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

        [Test]
        public void RouteThroughTheGatewayWhenTheArgForcesItOn()
        {
            InitializeFeatureFlags(optimizedAssets: false, useGateway: false);
            var urlsSource = new GatewayUrlsSource(DecentralandEnvironment.Org, new IRealmData.Fake(), ILaunchMode.PLAY, cliUseGateway: true);

            Assert.AreEqual("https://gateway.decentraland.org/places/api/places", urlsSource.Url(DecentralandUrl.ApiPlaces));
        }

        [Test]
        public void KeepUrlsOffTheGatewayWhenTheArgForcesItOff()
        {
            InitializeFeatureFlags(optimizedAssets: false, useGateway: true);
            var urlsSource = new GatewayUrlsSource(DecentralandEnvironment.Org, new IRealmData.Fake(), ILaunchMode.PLAY, cliUseGateway: false);

            Assert.AreEqual("https://places.decentraland.org/api/places", urlsSource.Url(DecentralandUrl.ApiPlaces));
        }

        // The arg overrides the flag, never the environment: today has no gateway to route to.
        [Test]
        public void KeepTodayOffTheGatewayWhenTheArgForcesItOn()
        {
            InitializeFeatureFlags(optimizedAssets: false, useGateway: false);
            var urlsSource = new GatewayUrlsSource(DecentralandEnvironment.Today, new IRealmData.Fake(), ILaunchMode.PLAY, cliUseGateway: true);

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
        public void KeepNonEnvShapedDecentralandHostsOffTheGateway(string customHost)
        {
            InitializeFeatureFlags(optimizedAssets: false, useGateway: true);
            var urlsSource = new GatewayUrlsSource(DecentralandEnvironment.Org, new IRealmData.Fake(), ILaunchMode.PLAY, cliGatekeeperUrl: customHost);

            Assert.AreEqual($"{customHost}/get-scene-adapter", urlsSource.Url(DecentralandUrl.GateKeeperSceneAdapter));
        }
    }
}
