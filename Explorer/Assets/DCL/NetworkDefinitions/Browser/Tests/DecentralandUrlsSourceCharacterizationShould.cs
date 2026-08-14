using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility;
using ECS;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;

namespace DCL.Browser.DecentralandUrls.Tests
{
    /// <summary>
    ///     Characterization coverage for the whole DecentralandUrl surface, added before
    ///     generalizing the hardcoded "decentraland" domain. Every value that any caller
    ///     resolves through IDecentralandUrlsSource is pinned here, so a domain change that
    ///     alters (or fails to alter) a URL is caught. Golden values are derived from the
    ///     URL templates independently of the runtime resolution logic.
    /// </summary>
    public class DecentralandUrlsSourceCharacterizationShould
    {
        [SetUp]
        public void SetUp() => FeatureFlagsConfiguration.Reset();

        [TearDown]
        public void TearDown() => FeatureFlagsConfiguration.Reset();

        private static void InitFlags(bool useGateway = false)
        {
            FeatureFlagsConfiguration.Reset();
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(new FeatureFlagsResultDto
            {
                flags = new Dictionary<string, bool>
                {
                    [FeatureFlagsStrings.OPTIMIZED_ASSETS] = true,
                    [FeatureFlagsStrings.USE_GATEWAY] = useGateway,
                },
                variants = new Dictionary<string, FeatureFlagVariantDto>(),
            }));
        }

        /// <summary>
        ///     The boot window before remote flags arrive: the singleton is initialized but holds
        ///     an empty result, so IsEmpty is true.
        /// </summary>
        private static void InitEmptyFlags()
        {
            FeatureFlagsConfiguration.Reset();
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
        }

        // ---- Domain-bearing URLs: exactly one thing varies by environment — the domain ----

        private static IEnumerable DomainBearingCases()
        {
            yield return new TestCaseData(DecentralandUrl.Account, "https://decentraland.org/account/", "https://decentraland.zone/account/");
            yield return new TestCaseData(DecentralandUrl.ActiveCommunityVoiceChats, "https://social-api.decentraland.org/v1/community-voice-chats/active", "https://social-api.decentraland.zone/v1/community-voice-chats/active");
            yield return new TestCaseData(DecentralandUrl.ApiAuth, "https://auth-api.decentraland.org", "https://auth-api.decentraland.zone");
            yield return new TestCaseData(DecentralandUrl.ApiChunks, "https://api.decentraland.org/v1/map.png", "https://api.decentraland.zone/v1/map.png");
            yield return new TestCaseData(DecentralandUrl.ApiDestinations, "https://places.decentraland.org/api/destinations", "https://places.decentraland.zone/api/destinations");
            yield return new TestCaseData(DecentralandUrl.ApiEvents, "https://events.decentraland.org/api/events", "https://events.decentraland.zone/api/events");
            yield return new TestCaseData(DecentralandUrl.ApiFriends, "wss://rpc-social-service-ea.decentraland.org", "wss://rpc-social-service-ea.decentraland.zone");
            yield return new TestCaseData(DecentralandUrl.ApiPlaces, "https://places.decentraland.org/api/places", "https://places.decentraland.zone/api/places");
            yield return new TestCaseData(DecentralandUrl.ApiRpc, "wss://rpc.decentraland.org", "wss://rpc.decentraland.zone");
            yield return new TestCaseData(DecentralandUrl.ApiWorlds, "https://places.decentraland.org/api/worlds", "https://places.decentraland.zone/api/worlds");
            yield return new TestCaseData(DecentralandUrl.ArchipelagoHotScenes, "https://archipelago-ea-stats.decentraland.org/hot-scenes", "https://archipelago-ea-stats.decentraland.zone/hot-scenes");
            yield return new TestCaseData(DecentralandUrl.ArchipelagoStatus, "https://archipelago-ea-stats.decentraland.org/status", "https://archipelago-ea-stats.decentraland.zone/status");
            yield return new TestCaseData(DecentralandUrl.AuthSignatureWebApp, "https://decentraland.org/auth/requests", "https://decentraland.zone/auth/requests");
            yield return new TestCaseData(DecentralandUrl.Badges, "https://badges.decentraland.org", "https://badges.decentraland.zone");
            yield return new TestCaseData(DecentralandUrl.Blocklist, "https://config.decentraland.org/denylist.json", "https://config.decentraland.zone/denylist.json");
            yield return new TestCaseData(DecentralandUrl.BuilderApiContent, "https://builder-api.decentraland.org/v1/storage/contents/", "https://builder-api.decentraland.zone/v1/storage/contents/");
            yield return new TestCaseData(DecentralandUrl.BuilderApiDtos, "https://builder-api.decentraland.org/v1/collections/[COL-ID]/items", "https://builder-api.decentraland.zone/v1/collections/[COL-ID]/items");
            yield return new TestCaseData(DecentralandUrl.BuilderApiNewsletter, "https://builder-api.decentraland.org/v1/newsletter", "https://builder-api.decentraland.zone/v1/newsletter");
            yield return new TestCaseData(DecentralandUrl.CameraReelImages, "https://camera-reel-service.decentraland.org/api/images", "https://camera-reel-service.decentraland.zone/api/images");
            yield return new TestCaseData(DecentralandUrl.CameraReelLink, "https://reels.decentraland.org", "https://reels.decentraland.zone");
            yield return new TestCaseData(DecentralandUrl.CameraReelPlaces, "https://camera-reel-service.decentraland.org/api/places", "https://camera-reel-service.decentraland.zone/api/places");
            yield return new TestCaseData(DecentralandUrl.CameraReelUsers, "https://camera-reel-service.decentraland.org/api/users", "https://camera-reel-service.decentraland.zone/api/users");
            yield return new TestCaseData(DecentralandUrl.ChatTranslate, "https://autotranslate-server.decentraland.org/translate", "https://autotranslate-server.decentraland.zone/translate");
            yield return new TestCaseData(DecentralandUrl.CodeOfEthics, "https://decentraland.org/ethics", "https://decentraland.zone/ethics");
            yield return new TestCaseData(DecentralandUrl.Communities, "https://social-api.decentraland.org/v1/communities", "https://social-api.decentraland.zone/v1/communities");
            yield return new TestCaseData(DecentralandUrl.CommunitiesV2, "https://social-api.decentraland.org/v2/communities", "https://social-api.decentraland.zone/v2/communities");
            yield return new TestCaseData(DecentralandUrl.CommunityProfileLink, "https://decentraland.org/social/communities/{0}?utm_org=dcl&utm_source=explorer&utm_medium=organic&utm_campaign=communities", "https://decentraland.zone/social/communities/{0}?utm_org=dcl&utm_source=explorer&utm_medium=organic&utm_campaign=communities");
            yield return new TestCaseData(DecentralandUrl.CommunityThumbnail, "https://assets-cdn.decentraland.org/social/communities/{0}/raw-thumbnail.png", "https://assets-cdn.decentraland.zone/social/communities/{0}/raw-thumbnail.png");
            yield return new TestCaseData(DecentralandUrl.ContentModerationReport, "https://places.decentraland.org/api/report", "https://places.decentraland.zone/api/report");
            yield return new TestCaseData(DecentralandUrl.ContentPolicy, "https://decentraland.org/content", "https://decentraland.zone/content");
            yield return new TestCaseData(DecentralandUrl.CreatorHub, "https://decentraland.org/create/", "https://decentraland.zone/create/");
            yield return new TestCaseData(DecentralandUrl.DAO, "https://decentraland.org/dao/", "https://decentraland.zone/dao/");
            yield return new TestCaseData(DecentralandUrl.Discord, "https://decentraland.org/discord/", "https://decentraland.zone/discord/");
            yield return new TestCaseData(DecentralandUrl.Faqs, "https://docs.decentraland.org/faqs/decentraland-101", "https://docs.decentraland.zone/faqs/decentraland-101");
            yield return new TestCaseData(DecentralandUrl.Genesis, "https://realm-provider-ea.decentraland.org/main", "https://realm-provider-ea.decentraland.zone/main");
            yield return new TestCaseData(DecentralandUrl.GoShoppingWithMarketplaceCredits, "https://decentraland.org/marketplace/browse?sortBy=newest&status=on_sale&withCredits=true", "https://decentraland.zone/marketplace/browse?sortBy=newest&status=on_sale&withCredits=true");
            yield return new TestCaseData(DecentralandUrl.Help, "https://decentraland.org/help/", "https://decentraland.zone/help/");
            yield return new TestCaseData(DecentralandUrl.Host, "https://decentraland.org", "https://decentraland.zone");
            yield return new TestCaseData(DecentralandUrl.JumpInGenesisCityLink, "https://decentraland.org/jump/?position={0},{1}", "https://decentraland.zone/jump/?position={0},{1}");
            yield return new TestCaseData(DecentralandUrl.JumpInWorldLink, "https://decentraland.org/jump/?realm={0}", "https://decentraland.zone/jump/?realm={0}");
            yield return new TestCaseData(DecentralandUrl.Map, "https://places.decentraland.org/api/map", "https://places.decentraland.zone/api/map");
            yield return new TestCaseData(DecentralandUrl.Market, "https://market.decentraland.org", "https://market.decentraland.zone");
            yield return new TestCaseData(DecentralandUrl.MarketplaceClaimName, "https://decentraland.org/marketplace/names/claim", "https://decentraland.zone/marketplace/names/claim");
            yield return new TestCaseData(DecentralandUrl.MarketplaceCredits, "https://credits.decentraland.org", "https://credits.decentraland.zone");
            yield return new TestCaseData(DecentralandUrl.MarketplaceLink, "https://decentraland.org/marketplace", "https://decentraland.zone/marketplace");
            yield return new TestCaseData(DecentralandUrl.MarketplaceServer, "https://marketplace-api.decentraland.org", "https://marketplace-api.decentraland.zone");
            yield return new TestCaseData(DecentralandUrl.MediaConverter, "https://metamorph-api.decentraland.org/convert?url={0}", "https://metamorph-api.decentraland.zone/convert?url={0}");
            yield return new TestCaseData(DecentralandUrl.Members, "https://social-api.decentraland.org/v1/members", "https://social-api.decentraland.zone/v1/members");
            yield return new TestCaseData(DecentralandUrl.MembersV2, "https://social-api.decentraland.org/v2/members", "https://social-api.decentraland.zone/v2/members");
            yield return new TestCaseData(DecentralandUrl.MetaTransactionServer, "https://transactions-api.decentraland.org/v1/transactions", "https://transactions-api.decentraland.zone/v1/transactions");
            yield return new TestCaseData(DecentralandUrl.MinimumSpecs, "https://docs.decentraland.org/player/FAQs/decentraland-101/#what-hardware-do-i-need-to-run-decentraland", "https://docs.decentraland.zone/player/FAQs/decentraland-101/#what-hardware-do-i-need-to-run-decentraland");
            yield return new TestCaseData(DecentralandUrl.Notifications, "https://notifications.decentraland.org", "https://notifications.decentraland.zone");
            yield return new TestCaseData(DecentralandUrl.OpenSea, "https://opensea.decentraland.org", "https://opensea.decentraland.zone");
            yield return new TestCaseData(DecentralandUrl.POI, "https://dcl-lists.decentraland.org/pois", "https://dcl-lists.decentraland.zone/pois");
            yield return new TestCaseData(DecentralandUrl.PeerAbout, "https://peer.decentraland.org/about", "https://peer.decentraland.zone/about");
            yield return new TestCaseData(DecentralandUrl.PeerContent, "https://peer.decentraland.org/content/contents", "https://peer.decentraland.zone/content/contents");
            yield return new TestCaseData(DecentralandUrl.PrivacyPolicy, "https://decentraland.org/privacy", "https://decentraland.zone/privacy");
            yield return new TestCaseData(DecentralandUrl.Pulse, "pulse-server.decentraland.org", "pulse-server.decentraland.zone");
            yield return new TestCaseData(DecentralandUrl.RemotePeers, "https://archipelago-ea-stats.decentraland.org/comms/peers", "https://archipelago-ea-stats.decentraland.zone/comms/peers");
            yield return new TestCaseData(DecentralandUrl.RemotePeersWorld, "https://worlds-content-server.decentraland.org/wallet/[USER-ID]/connected-world", "https://worlds-content-server.decentraland.zone/wallet/[USER-ID]/connected-world");
            yield return new TestCaseData(DecentralandUrl.ReportUserForm, "https://decentraland.org/report/players?player_address={0}&reported_address={1}", "https://decentraland.zone/report/players?player_address={0}&reported_address={1}");
            yield return new TestCaseData(DecentralandUrl.Servers, "https://peer.decentraland.org/lambdas/contracts/servers", "https://peer.decentraland.zone/lambdas/contracts/servers");
            yield return new TestCaseData(DecentralandUrl.ShopLink, "https://decentraland.org/shop", "https://decentraland.zone/shop");
            yield return new TestCaseData(DecentralandUrl.SocialServiceMutes, "https://social-api.decentraland.org/v1/mutes", "https://social-api.decentraland.zone/v1/mutes");
            yield return new TestCaseData(DecentralandUrl.Support, "https://docs.decentraland.org/player/support/", "https://docs.decentraland.zone/player/support/");
            yield return new TestCaseData(DecentralandUrl.SupportLink, "https://decentraland.org/help/", "https://decentraland.zone/help/");
            yield return new TestCaseData(DecentralandUrl.TermsOfUse, "https://decentraland.org/terms", "https://decentraland.zone/terms");
            yield return new TestCaseData(DecentralandUrl.WhatsOnEventLink, "https://decentraland.org/whats-on/?id={0}", "https://decentraland.zone/whats-on/?id={0}");
            yield return new TestCaseData(DecentralandUrl.WhatsOnNewEventLink, "https://decentraland.org/whats-on/new-event", "https://decentraland.zone/whats-on/new-event");
            yield return new TestCaseData(DecentralandUrl.WorldComms, "https://worlds-content-server.decentraland.org/worlds/{0}/comms", "https://worlds-content-server.decentraland.zone/worlds/{0}/comms");
            yield return new TestCaseData(DecentralandUrl.WorldCommsAdapter, "https://worlds-content-server.decentraland.org/worlds/{0}/scenes/{1}/comms", "https://worlds-content-server.decentraland.zone/worlds/{0}/scenes/{1}/comms");
            yield return new TestCaseData(DecentralandUrl.WorldContentServer, "https://worlds-content-server.decentraland.org/contents/", "https://worlds-content-server.decentraland.zone/contents/");
            yield return new TestCaseData(DecentralandUrl.WorldPermissions, "https://worlds-content-server.decentraland.org/world/{0}/permissions", "https://worlds-content-server.decentraland.zone/world/{0}/permissions");
            yield return new TestCaseData(DecentralandUrl.WorldServer, "https://worlds-content-server.decentraland.org/world", "https://worlds-content-server.decentraland.zone/world");
        }

        [TestCaseSource(nameof(DomainBearingCases))]
        public void ResolveDomainBearingUrlPerEnvironment(DecentralandUrl url, string expectedOrg, string expectedZone)
        {
            InitFlags();
            Assert.AreEqual(expectedOrg, DecentralandUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY).Url(url), $"{url} (org)");
            Assert.AreEqual(expectedZone, DecentralandUrlsSource.CreateForTest(DecentralandEnvironment.Zone, ILaunchMode.PLAY).Url(url), $"{url} (zone)");
        }

        // ---- Env-independent URLs: external hosts (and .org-pinned links) must never move ----

        private static IEnumerable EnvIndependentCases()
        {
            yield return new TestCaseData(DecentralandUrl.DecentralandWorlds, "https://decentraland.org/blog/about-decentraland/decentraland-worldsTake -your-own-virtual-space?utm_org=dcl&utm_source=explorer&utm_medium=organic");
            yield return new TestCaseData(DecentralandUrl.DiscordDirectLink, "https://discord.gg/decentraland");
            yield return new TestCaseData(DecentralandUrl.ManaUsdRateApiUrl, "https://api.coingecko.com/api/v3/simple/price?ids=decentraland&vs_currencies=usd");
            yield return new TestCaseData(DecentralandUrl.NewsletterSubscriptionLink, "https://decentraland.beehiiv.com/?utm_org=dcl&utm_source=client&utm_medium=organic&utm_campaign=marketplacecredits&utm_term=trialend");
            yield return new TestCaseData(DecentralandUrl.TwitterLink, "https://x.com/decentraland");
            yield return new TestCaseData(DecentralandUrl.TwitterNewPostLink, "https://twitter.com/intent/tweet?text={0}&hashtags={1}&url={2}");
        }

        [TestCaseSource(nameof(EnvIndependentCases))]
        public void ResolveEnvIndependentUrlIdenticallyEverywhere(DecentralandUrl url, string expected)
        {
            InitFlags();
            Assert.AreEqual(expected, DecentralandUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY).Url(url), $"{url} (org)");
            Assert.AreEqual(expected, DecentralandUrlsSource.CreateForTest(DecentralandEnvironment.Zone, ILaunchMode.PLAY).Url(url), $"{url} (zone)");
        }

        // ---- Gateway routing: every supported service URL funnels through gateway.<domain> ----

        private static IEnumerable<DecentralandUrl> GatewaySupportedUrls()
        {
            yield return DecentralandUrl.ApiPlaces;
            yield return DecentralandUrl.ApiWorlds;
            yield return DecentralandUrl.ApiDestinations;
            yield return DecentralandUrl.Map;
            yield return DecentralandUrl.ContentModerationReport;
            yield return DecentralandUrl.ApiAuth;
            yield return DecentralandUrl.ApiChunks;
            yield return DecentralandUrl.GateKeeperSceneAdapter;
            yield return DecentralandUrl.ChatAdapter;
            yield return DecentralandUrl.GatekeeperStatus;
            yield return DecentralandUrl.BannedUsers;
            yield return DecentralandUrl.RemotePeers;
            yield return DecentralandUrl.RemotePeersWorld;
            yield return DecentralandUrl.ArchipelagoStatus;
            yield return DecentralandUrl.ArchipelagoHotScenes;
            yield return DecentralandUrl.WorldContentServer;
            yield return DecentralandUrl.Genesis;
            yield return DecentralandUrl.Badges;
            yield return DecentralandUrl.CameraReelImages;
            yield return DecentralandUrl.CameraReelPlaces;
            yield return DecentralandUrl.CameraReelUsers;
            yield return DecentralandUrl.MediaConverter;
            yield return DecentralandUrl.MarketplaceCredits;
            yield return DecentralandUrl.Notifications;
            yield return DecentralandUrl.CommunityThumbnail;
            yield return DecentralandUrl.Communities;
            yield return DecentralandUrl.CommunitiesV2;
            yield return DecentralandUrl.Members;
            yield return DecentralandUrl.MembersV2;
            yield return DecentralandUrl.ActiveCommunityVoiceChats;
        }

        [TestCaseSource(nameof(GatewaySupportedUrls))]
        public void RouteSupportedUrlThroughGateway(DecentralandUrl url)
        {
            InitFlags(useGateway: true);
            string routed = GatewayUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY).Url(url);
            StringAssert.StartsWith("https://gateway.decentraland.org/", routed, $"{url} should route through the gateway host");
        }

        [TestCaseSource(nameof(GatewaySupportedUrls))]
        public void ReverseGatewayRoutingBackToOrigin(DecentralandUrl url)
        {
            InitFlags(useGateway: true);
            GatewayUrlsSource gateway = GatewayUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY);
            DecentralandUrlsSource plain = DecentralandUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY);
            string routed = gateway.Url(url);
            Assert.AreEqual(plain.Url(url), gateway.GetOriginalUrl(routed), $"{url} round-trip");
        }

        [Test]
        public void LeaveUrlsUntransformedWhenGatewayDisabled()
        {
            InitFlags(useGateway: false);
            GatewayUrlsSource gateway = GatewayUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY);
            DecentralandUrlsSource plain = DecentralandUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY);
            Assert.AreEqual(plain.Url(DecentralandUrl.ApiPlaces), gateway.Url(DecentralandUrl.ApiPlaces));
        }

        [Test]
        public void RouteGenesisThroughGatewayWhenFlagsLoadAfterFirstResolution()
        {
            InitEmptyFlags();
            GatewayUrlsSource gateway = GatewayUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY);

            Assert.AreEqual("https://realm-provider-ea.decentraland.org/main", gateway.Url(DecentralandUrl.Genesis), "pre-flags URL must be fully resolved");

            InitFlags(useGateway: true);

            Assert.AreEqual("https://gateway.decentraland.org/realm-provider-ea/main", gateway.Url(DecentralandUrl.Genesis), "post-flags URL must route through the gateway");
        }

        [Test]
        public void KeepGenesisRawAndStableWhenGatewayFlagDisabled()
        {
            InitFlags(useGateway: false);
            GatewayUrlsSource gateway = GatewayUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY);

            Assert.AreEqual("https://realm-provider-ea.decentraland.org/main", gateway.Url(DecentralandUrl.Genesis));
            Assert.AreEqual("https://realm-provider-ea.decentraland.org/main", gateway.Url(DecentralandUrl.Genesis), "repeated resolution must be stable");
        }

        [Test]
        public void LeaveThirdPartyHostsUntouchedByTransformUrl()
        {
            InitFlags(useGateway: true);
            GatewayUrlsSource gateway = GatewayUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY);
            Assert.AreEqual("https://example.com/x", gateway.TransformUrl("https://example.com/x"));
        }

        // ---- Custom base domain: every domain-bearing host retargets; nothing else moves ----

        private const string CUSTOM_DOMAIN = "interconnected.online";

        [TestCaseSource(nameof(DomainBearingCases))]
        public void RetargetDomainBearingUrlToCustomDomain(DecentralandUrl url, string expectedOrg, string expectedZone)
        {
            InitFlags();
            string expected = expectedOrg.Replace("decentraland.org", CUSTOM_DOMAIN);
            Assert.AreEqual(expected, DecentralandUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY, CUSTOM_DOMAIN).Url(url), $"{url} (custom domain)");
        }

        [TestCaseSource(nameof(EnvIndependentCases))]
        public void LeaveEnvIndependentUrlUnchangedUnderCustomDomain(DecentralandUrl url, string expected)
        {
            InitFlags();
            Assert.AreEqual(expected, DecentralandUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY, CUSTOM_DOMAIN).Url(url), $"{url} (custom-domain invariant)");
        }

        [TestCaseSource(nameof(GatewaySupportedUrls))]
        public void RouteSupportedUrlThroughCustomGatewayDomain(DecentralandUrl url)
        {
            InitFlags(useGateway: true);
            string routed = GatewayUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY, CUSTOM_DOMAIN).Url(url);
            StringAssert.StartsWith($"https://gateway.{CUSTOM_DOMAIN}/", routed, $"{url} should route through the custom gateway host");
        }

        [Test]
        public void RouteGenesisThroughCustomGatewayDomainWhenFlagsLoadAfterFirstResolution()
        {
            InitEmptyFlags();
            GatewayUrlsSource gateway = GatewayUrlsSource.CreateForTest(DecentralandEnvironment.Org, ILaunchMode.PLAY, CUSTOM_DOMAIN);

            Assert.AreEqual($"https://realm-provider-ea.{CUSTOM_DOMAIN}/main", gateway.Url(DecentralandUrl.Genesis), "pre-flags URL must be fully resolved");

            InitFlags(useGateway: true);

            Assert.AreEqual($"https://gateway.{CUSTOM_DOMAIN}/realm-provider-ea/main", gateway.Url(DecentralandUrl.Genesis), "post-flags URL must route through the custom gateway");
        }
    }
}
