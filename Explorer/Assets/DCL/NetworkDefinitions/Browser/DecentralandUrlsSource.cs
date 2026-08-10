using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility;
using ECS;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Pool;

// ReSharper disable once CheckNamespace
namespace DCL.Browser.DecentralandUrls
{
    public class DecentralandUrlsSource : IDecentralandUrlsSource
    {
        protected enum CacheBehaviour
        {
            /// <summary>
            ///     URL is static and can be safely cached
            /// </summary>
            Static = 0,

            /// <summary>
            ///     URL should be invalidated upon realm change
            /// </summary>
            RealmDependent = 1,

            /// <summary>
            ///     URL can't be cached if FF are not yet configured
            /// </summary>
            FeatureFlagsDependent = 2,
        }

        protected const string ENV = "{ENV}";
        private const string SCENE_ADAPTER_PATH = "/get-scene-adapter";

        private static readonly string FEATURE_FLAGS_RAW_URL = $"https://feature-flags.decentraland.{ENV}";

        private readonly Dictionary<DecentralandUrl, UrlData> cache = new ();
        private readonly IRealmData realmData;
        private readonly ILaunchMode launchMode;
        private readonly string decentralandDomain;
        private readonly string? gatekeeperBaseOverride;
        private readonly string? optimizedAssetsBaseOverride;
        private readonly bool isTodayEnvironment;

        public DecentralandUrlsSource(
            DecentralandEnvironment environment,
            IRealmData realmData,
            ILaunchMode launchMode,
            GatekeeperMode gatekeeperMode = GatekeeperMode.Org,
            string customGatekeeperUrl = "",
            string? cliGatekeeperUrl = null,
            string? cliOptimizedAssetsUrl = null)
        {
            decentralandDomain = environment.ToString()!.ToLower();
            isTodayEnvironment = environment == DecentralandEnvironment.Today;
            this.realmData = realmData;
            this.launchMode = launchMode;
            gatekeeperBaseOverride = ResolveGatekeeperOverride(gatekeeperMode, customGatekeeperUrl, cliGatekeeperUrl, out string source);
            ReportHub.Log(ReportCategory.STARTUP, $"Gatekeeper base override: {gatekeeperBaseOverride ?? "(default)"} (source: {source})");
            optimizedAssetsBaseOverride = cliOptimizedAssetsUrl?.TrimEnd('/');

            if (isTodayEnvironment)
            {
                // The today environment is a mixture of the org and today environments.
                // Asset delivery (registry and S3) are used with the `.today` extension
                // Adapter info (both scene and room) also have to responde to the `.today` environment
                // Archipelago status as well, to have a clear minimap
                // All the remaining urls should use the `Org` domain, that's why we change the domain to forcefully `.org`
                // It's a catalyst that replicates the org environment and eth network, but doesn't propagate back to the production catalysts
                Url(DecentralandUrl.AssetBundleRegistry);
                Url(DecentralandUrl.AssetBundleRegistryVersion);
                Url(DecentralandUrl.AssetBundlesCDN);
                Url(DecentralandUrl.Profiles);
                Url(DecentralandUrl.ProfilesMetadata);
                Url(DecentralandUrl.EntitiesActive);
                Url(DecentralandUrl.EntitiesActiveElements);
                Url(DecentralandUrl.WorldEntitiesActive);
                Url(DecentralandUrl.ArchipelagoStatus);
                Url(DecentralandUrl.ArchipelagoHotScenes);
                Url(DecentralandUrl.Genesis);
                Url(DecentralandUrl.Gatekeeper);
                Url(DecentralandUrl.GateKeeperSceneAdapter);
                Url(DecentralandUrl.LocalGateKeeperSceneAdapter);
                Url(DecentralandUrl.ChatAdapter);
                Url(DecentralandUrl.GatekeeperStatus);
                Url(DecentralandUrl.BannedUsers);
                Url(DecentralandUrl.SceneAdmins);
                Url(DecentralandUrl.RemotePeers);
                decentralandDomain = nameof(DecentralandEnvironment.Org).ToLower();
            }

            realmData.RealmType.OnUpdate += ResetRealmDependentUrls;
        }

        private static string? ResolveGatekeeperOverride(GatekeeperMode mode, string customUrl, string? cliOverride, out string source)
        {
            if (!string.IsNullOrEmpty(cliOverride))
            {
                source = "CLI";
                return cliOverride;
            }

            source = mode.ToString();

            return mode switch
                   {
                       GatekeeperMode.Org => null,
                       GatekeeperMode.Zone => "https://comms-gatekeeper." + IDecentralandUrlsSource.ZONE_DOMAIN,
                       GatekeeperMode.Today => "https://comms-gatekeeper." + IDecentralandUrlsSource.TODAY_DOMAIN,
                       GatekeeperMode.Localhost => "http://localhost:3000",
                       GatekeeperMode.Custom => string.IsNullOrEmpty(customUrl) ? null : customUrl,
                       _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
                   };
        }

        /// <summary>
        ///     Creates a fully irrelevant stub
        /// </summary>
        public static DecentralandUrlsSource CreateForTest() =>
            new (DecentralandEnvironment.Zone, new IRealmData.Fake(), ILaunchMode.PLAY);

        public static DecentralandUrlsSource CreateForTest(DecentralandEnvironment environment, ILaunchMode launchMode) =>
            new (environment, new IRealmData.Fake(), launchMode);

        public string Probe(DecentralandUrl decentralandUrl)
        {
            if (cache.TryGetValue(decentralandUrl, out UrlData cached))
                return cached.Url!;

            UrlData rawUrl = RawUrl(decentralandUrl);

            return Probe(rawUrl.ToString(), decentralandDomain);
        }

        private static string Probe(string rawUrl, string environment) =>
            rawUrl.Replace(ENV, environment);

        public string Url(DecentralandUrl decentralandUrl)
        {
            const string REALM_DEPENDENT = "<REALM_DEPENDENT>";
            const string FEATURE_FLAG_DEPENDENT = "<FEATURE_FLAG_DEPENDENT>";

            if (!cache.TryGetValue(decentralandUrl, out UrlData urlData))
            {
                urlData = RawUrl(decentralandUrl);

                switch (urlData.Caching)
                {
                    case CacheBehaviour.RealmDependent when !realmData.Configured:
                        return REALM_DEPENDENT;

                    case CacheBehaviour.FeatureFlagsDependent when FeatureFlagsConfiguration.Instance.IsEmpty:
                        return urlData.Url ?? FEATURE_FLAG_DEPENDENT;

                    default:
                        urlData = new UrlData(urlData.Caching, urlData.Url!.Replace(ENV, decentralandDomain));
                        cache[decentralandUrl] = urlData;
                        break;
                }
            }

            return urlData.Url!;
        }

        public virtual string TransformUrl(string originalUrl) =>
            originalUrl;

        public virtual string GetOriginalUrl(string url) =>
            url;

        public string GetHostnameForFeatureFlag() =>
            launchMode.CurrentMode switch
            {
                LaunchMode.Play => Url(DecentralandUrl.Host),
                LaunchMode.LocalSceneDevelopment => "localhost", //TODO should this behaviour be extracted to Url() call?
                _ => throw new ArgumentOutOfRangeException(),
            };

        private void ResetRealmDependentUrls(RealmKind realmKind)
        {
            using PooledObject<List<DecentralandUrl>> _ = ListPool<DecentralandUrl>.Get(out List<DecentralandUrl>? realmDependentCachedUrls);

            realmDependentCachedUrls.AddRange(cache.Where(kvp => kvp.Value.Caching == CacheBehaviour.RealmDependent).Select(kvp => kvp.Key));

            foreach (DecentralandUrl url in realmDependentCachedUrls)
                cache.Remove(url);
        }

        private string ResolveGatekeeperBaseUrl(string defaultBaseUrl) =>
            gatekeeperBaseOverride ?? defaultBaseUrl;

        /// <summary>
        ///     The "--optimized-assets-url" arg or the flag variant payload override the base url, otherwise
        ///     https://abcdn.decentraland.{ENV}. FeatureFlagsDependent means it is re-resolved (not cached) until flags load.
        /// </summary>
        private UrlData ResolveOptimizedAssetsUrl(string dedicatedHostUrl)
        {
            if (optimizedAssetsBaseOverride is { Length: > 0 })
                return new UrlData(CacheBehaviour.FeatureFlagsDependent, optimizedAssetsBaseOverride);

            FeatureFlagsConfiguration featureFlags = FeatureFlagsConfiguration.Instance;

            if (featureFlags.IsEmpty)
                return isTodayEnvironment
                    ? dedicatedHostUrl // Static — pinned on construction before the domain switches to org
                    : new UrlData(CacheBehaviour.FeatureFlagsDependent, dedicatedHostUrl.Replace(ENV, decentralandDomain));

            if (!featureFlags.IsEnabled(FeatureFlagsStrings.OPTIMIZED_ASSETS))
                return dedicatedHostUrl;

            if (featureFlags.TryGetTextPayload(FeatureFlagsStrings.OPTIMIZED_ASSETS, FeatureFlagsStrings.OPTIMIZED_ASSETS_BASE_URL_VARIANT, out string? customBaseUrl) && customBaseUrl is { Length: > 0 })
                return new UrlData(CacheBehaviour.FeatureFlagsDependent, customBaseUrl.TrimEnd('/'));

            return new UrlData(CacheBehaviour.FeatureFlagsDependent, $"https://abcdn.decentraland.{ENV}");
        }

        /// <summary>Registry-composed endpoints inherit the registry base's caching so a flag-driven base is not cached early.</summary>
        private UrlData ComposeRegistryUrl(string path) =>
            new (RawUrl(DecentralandUrl.AssetBundleRegistry).Caching, $"{Url(DecentralandUrl.AssetBundleRegistry)}{path}");

        public static string GetFeatureFlagsUrl(DecentralandEnvironment env) =>
            Probe(FEATURE_FLAGS_RAW_URL, env.ToString().ToLower());

        protected virtual UrlData RawUrl(DecentralandUrl decentralandUrl) =>
            decentralandUrl switch
            {
                DecentralandUrl.SupportLink => $"https://decentraland.{ENV}/help/",
                DecentralandUrl.DiscordDirectLink => "https://discord.gg/decentraland",
                DecentralandUrl.TwitterLink => "https://x.com/decentraland",
                DecentralandUrl.TwitterNewPostLink => "https://twitter.com/intent/tweet?text={0}&hashtags={1}&url={2}",
                DecentralandUrl.NewsletterSubscriptionLink => "https://decentraland.beehiiv.com/?utm_org=dcl&utm_source=client&utm_medium=organic&utm_campaign=marketplacecredits&utm_term=trialend",
                DecentralandUrl.MarketplaceLink => $"https://decentraland.{ENV}/marketplace",
                // Deliberately WITHOUT a query string: the passport builds an item URL by appending
                // "/item/{contract}/{id}" to this value, and a `?` here would put the path after the
                // query and produce a link that 404s. Callers that OPEN this url tag it themselves —
                // see DecentralandUrlExtensions.WithClientSource.
                DecentralandUrl.ShopLink => $"https://decentraland.{ENV}/shop",
                DecentralandUrl.MarketplaceServer => $"https://marketplace-api.decentraland.{ENV}",
                DecentralandUrl.PrivacyPolicy => $"https://decentraland.{ENV}/privacy",
                DecentralandUrl.TermsOfUse => $"https://decentraland.{ENV}/terms",
                DecentralandUrl.ContentPolicy => $"https://decentraland.{ENV}/content",
                DecentralandUrl.CodeOfEthics => $"https://decentraland.{ENV}/ethics",
                DecentralandUrl.ApiPlaces => $"https://places.decentraland.{ENV}/api/places",
                DecentralandUrl.ApiWorlds => $"https://places.decentraland.{ENV}/api/worlds",
                DecentralandUrl.ApiDestinations => $"https://places.decentraland.{ENV}/api/destinations",
                DecentralandUrl.ApiAuth => $"https://auth-api.decentraland.{ENV}",
                DecentralandUrl.ApiRpc => $"wss://rpc.decentraland.{ENV}",
                DecentralandUrl.MetaTransactionServer => $"https://transactions-api.decentraland.{ENV}/v1/transactions",
                DecentralandUrl.AuthSignatureWebApp => $"https://decentraland.{ENV}/auth/requests",
                DecentralandUrl.BuilderApiDtos => $"https://builder-api.decentraland.{ENV}/v1/collections/[COL-ID]/items",
                DecentralandUrl.BuilderApiContent => $"https://builder-api.decentraland.{ENV}/v1/storage/contents/",
                DecentralandUrl.BuilderApiNewsletter => $"https://builder-api.decentraland.{ENV}/v1/newsletter",
                DecentralandUrl.POI => $"https://dcl-lists.decentraland.{ENV}/pois",
                DecentralandUrl.Map => $"https://places.decentraland.{ENV}/api/map",
                DecentralandUrl.ContentModerationReport => $"https://places.decentraland.{ENV}/api/report",
                DecentralandUrl.Gatekeeper => ResolveGatekeeperBaseUrl($"https://comms-gatekeeper.decentraland.{ENV}"),
                DecentralandUrl.GateKeeperSceneAdapter => $"{RawUrl(DecentralandUrl.Gatekeeper).Url!}{SCENE_ADAPTER_PATH}",
                DecentralandUrl.LocalGateKeeperSceneAdapter => $"{ResolveGatekeeperBaseUrl("https://comms-gatekeeper-local." + IDecentralandUrlsSource.ORG_DOMAIN)}{SCENE_ADAPTER_PATH}",
                DecentralandUrl.ChatAdapter => $"{RawUrl(DecentralandUrl.Gatekeeper).Url!}/private-messages/token",
                DecentralandUrl.ApiEvents => $"https://events.decentraland.{ENV}/api/events",
                DecentralandUrl.WhatsOnNewEventLink => $"https://decentraland.{ENV}/whats-on/new-event",
                DecentralandUrl.WhatsOnEventLink => $"https://decentraland.{ENV}/whats-on/?id={{0}}",
                DecentralandUrl.OpenSea => $"https://opensea.decentraland.{ENV}",
                DecentralandUrl.Host => $"https://decentraland.{ENV}",
                DecentralandUrl.ApiChunks => $"https://api.decentraland.{ENV}/v1/map.png",
                DecentralandUrl.PeerAbout => $"https://peer.decentraland.{ENV}/about",
                DecentralandUrl.PeerContent => $"https://peer.decentraland.{ENV}/content/contents",
                DecentralandUrl.RemotePeers => $"https://archipelago-ea-stats.decentraland.{ENV}/comms/peers",
                DecentralandUrl.RemotePeersWorld => $"https://worlds-content-server.decentraland.{ENV}/wallet/[USER-ID]/connected-world",
                DecentralandUrl.DAO => $"https://decentraland.{ENV}/dao/",
                DecentralandUrl.FeatureFlags => FEATURE_FLAGS_RAW_URL,
                DecentralandUrl.Help => $"https://decentraland.{ENV}/help/",
                DecentralandUrl.Faqs => $"https://docs.decentraland.{ENV}/faqs/decentraland-101",
                DecentralandUrl.Discord => $"https://decentraland.{ENV}/discord/",
                DecentralandUrl.Account => $"https://decentraland.{ENV}/account/",
                DecentralandUrl.MinimumSpecs => $"https://docs.decentraland.{ENV}/player/FAQs/decentraland-101/#what-hardware-do-i-need-to-run-decentraland",
                DecentralandUrl.Market => $"https://market.decentraland.{ENV}",
                DecentralandUrl.AssetBundlesCDN => ResolveOptimizedAssetsUrl($"https://ab-cdn.decentraland.{ENV}"),
                DecentralandUrl.LodGeneratorCDN => ResolveOptimizedAssetsUrl($"https://lod-generator-unity-cdn.decentraland.{ENV}"),
                DecentralandUrl.ArchipelagoStatus => $"https://archipelago-ea-stats.decentraland.{ENV}/status",
                DecentralandUrl.ArchipelagoHotScenes => $"https://archipelago-ea-stats.decentraland.{ENV}/hot-scenes",
                DecentralandUrl.GatekeeperStatus => $"{RawUrl(DecentralandUrl.Gatekeeper).Url!}/status",
                DecentralandUrl.Genesis => $"https://realm-provider-ea.decentraland.{ENV}/main",
                DecentralandUrl.Badges => $"https://badges.decentraland.{ENV}",
                DecentralandUrl.CameraReelUsers => $"https://camera-reel-service.decentraland.{ENV}/api/users",
                DecentralandUrl.CameraReelImages => $"https://camera-reel-service.decentraland.{ENV}/api/images",
                DecentralandUrl.CameraReelPlaces => $"https://camera-reel-service.decentraland.{ENV}/api/places",
                DecentralandUrl.CameraReelLink => $"https://reels.decentraland.{ENV}",
                DecentralandUrl.Blocklist => $"https://config.decentraland.{ENV}/denylist.json",
                DecentralandUrl.ApiFriends => $"wss://rpc-social-service-ea.decentraland.{ENV}",
                DecentralandUrl.AssetBundleRegistry => ResolveOptimizedAssetsUrl($"https://asset-bundle-registry.decentraland.{ENV}"),

                DecentralandUrl.AssetBundleRegistryVersion => ComposeRegistryUrl("/entities/versions"),
                DecentralandUrl.MarketplaceClaimName => $"https://decentraland.{ENV}/marketplace/names/claim",
                DecentralandUrl.WorldPermissions => $"https://worlds-content-server.decentraland.{ENV}/world/{{0}}/permissions",
                DecentralandUrl.WorldComms => $"https://worlds-content-server.decentraland.{ENV}/worlds/{{0}}/comms",
                DecentralandUrl.WorldServer => $"https://worlds-content-server.decentraland.{ENV}/world",
                DecentralandUrl.WorldContentServer => $"https://worlds-content-server.decentraland.{ENV}/contents/",
                DecentralandUrl.Servers => $"https://peer.decentraland.{ENV}/lambdas/contracts/servers",
                DecentralandUrl.MediaConverter => $"https://metamorph-api.decentraland.{ENV}/convert?url={{0}}",
                DecentralandUrl.MarketplaceCredits => $"https://credits.decentraland.{ENV}",
                // Safe to tag on the constant, unlike ShopLink: nothing appends a path to this one, and it
                // already carries a query string so the separator is '&'.
                DecentralandUrl.GoShoppingWithMarketplaceCredits => $"https://decentraland.{ENV}/marketplace/browse?sortBy=newest&status=on_sale&withCredits=true&utm_source=client",
                DecentralandUrl.Notifications => $"https://notifications.decentraland.{ENV}",
                DecentralandUrl.Communities => $"https://social-api.decentraland.{ENV}/v1/communities",
                DecentralandUrl.CommunitiesV2 => $"https://social-api.decentraland.{ENV}/v2/communities",
                DecentralandUrl.CommunityThumbnail => $"https://assets-cdn.decentraland.{ENV}/social/communities/{{0}}/raw-thumbnail.png",
                DecentralandUrl.Members => $"https://social-api.decentraland.{ENV}/v1/members",
                DecentralandUrl.MembersV2 => $"https://social-api.decentraland.{ENV}/v2/members",
                DecentralandUrl.CommunityProfileLink => $"https://decentraland.{ENV}/social/communities/{{0}}?utm_org=dcl&utm_source=explorer&utm_medium=organic&utm_campaign=communities",
                DecentralandUrl.DecentralandWorlds => "https://decentraland.org/blog/about-decentraland/decentraland-worldsTake -your-own-virtual-space?utm_org=dcl&utm_source=explorer&utm_medium=organic",
                DecentralandUrl.ChatTranslate => $"https://autotranslate-server.decentraland.{ENV}/translate",
                DecentralandUrl.ActiveCommunityVoiceChats => $"https://social-api.decentraland.{ENV}/v1/community-voice-chats/active",
                DecentralandUrl.Support => $"https://docs.decentraland.{ENV}/player/support/",
                DecentralandUrl.CreatorHub => $"https://decentraland.{ENV}/create/",
                DecentralandUrl.ManaUsdRateApiUrl => "https://api.coingecko.com/api/v3/simple/price?ids=decentraland&vs_currencies=usd",
                DecentralandUrl.JumpInGenesisCityLink => $"https://decentraland.{ENV}/jump/?position={{0}},{{1}}",
                DecentralandUrl.JumpInWorldLink => $"https://decentraland.{ENV}/jump/?realm={{0}}",
                DecentralandUrl.ReportUserForm => $"https://decentraland.{ENV}/report/players?player_address={{0}}&reported_address={{1}}",
                DecentralandUrl.BannedUsers => $"{RawUrl(DecentralandUrl.Gatekeeper).Url!}/users/{{0}}/bans",
                DecentralandUrl.SceneAdmins => $"{RawUrl(DecentralandUrl.Gatekeeper).Url!}/scene-admin",
                DecentralandUrl.Pulse => $"pulse-server.decentraland.{ENV}",

                DecentralandUrl.Profiles => ComposeRegistryUrl("/profiles"),
                DecentralandUrl.ProfilesMetadata => ComposeRegistryUrl("/profiles/metadata"),
                DecentralandUrl.WorldCommsAdapter => $"https://worlds-content-server.decentraland.{ENV}/worlds/{{0}}/scenes/{{1}}/comms",

                DecentralandUrl.EntitiesActive => UrlData.RealmDependent(FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.ASSET_BUNDLE_FALLBACK) && launchMode.CurrentMode != LaunchMode.LocalSceneDevelopment ? $"{Url(DecentralandUrl.AssetBundleRegistry)}/entities/active" :
                    realmData.Configured ? realmData.Ipfs.EntitiesActiveEndpoint.Value : null),

                // Meant for Wearables and Emotes since they always must be solved by the AB-Registry
                DecentralandUrl.EntitiesActiveElements => ComposeRegistryUrl("/entities/active"),

                DecentralandUrl.WorldEntitiesActive => UrlData.RealmDependent(FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.ASSET_BUNDLE_FALLBACK) && launchMode.CurrentMode != LaunchMode.LocalSceneDevelopment ? $"{Url(DecentralandUrl.AssetBundleRegistry)}/entities/active?world_name={{0}}" :
                    realmData.Configured ? realmData.Ipfs.EntitiesActiveEndpoint.Value : null),

                DecentralandUrl.EntitiesDeployment => UrlData.RealmDependent(realmData.Configured ? realmData.Ipfs.EntitiesBaseUrl.Value : null),
                DecentralandUrl.Lambdas => UrlData.RealmDependent(realmData.Configured ? realmData.Ipfs.LambdasBaseUrl.Value : null),
                DecentralandUrl.Content => UrlData.RealmDependent(realmData.Configured ? realmData.Ipfs.ContentBaseUrl.Value : null),

                DecentralandUrl.SocialServiceMutes => $"https://social-api.decentraland.{ENV}/v1/mutes",

                _ => throw new ArgumentOutOfRangeException(nameof(decentralandUrl), decentralandUrl, null!),
            };

        protected readonly struct UrlData
        {
            public readonly CacheBehaviour Caching;
            public readonly string? Url;

            public UrlData(CacheBehaviour caching, string? url)
            {
                Caching = caching;
                Url = url;
            }

            public static UrlData RealmDependent(string? url) =>
                new (CacheBehaviour.RealmDependent, url);

            public static implicit operator UrlData(string rawUrl) =>
                new (CacheBehaviour.Static, rawUrl);

            public override string ToString() =>
                Url ?? "<NOT_CONFIGURED>";
        }
    }
}
