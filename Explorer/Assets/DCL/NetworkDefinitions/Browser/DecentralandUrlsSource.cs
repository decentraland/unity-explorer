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

        private const string SCENE_ADAPTER_PATH = "/get-scene-adapter";

        // The pre-login whitelist fetch resolves this host statically, before any instance exists
        // (see GetFeatureFlagsUrl), so the subdomain is shared rather than the whole url.
        private const string FEATURE_FLAGS_SUBDOMAIN = "feature-flags";

        // A base domain feeds host-trust checks, so anything that could turn it into a different authority
        // (scheme, userinfo, port, path) is rejected rather than silently accepted.
        private static readonly char[] BASE_DOMAIN_FORBIDDEN_CHARS = { '/', ':', '@', '?', '#', ' ', '\t' };

        private readonly Dictionary<DecentralandUrl, UrlData> cache = new ();
        private readonly IRealmData realmData;
        private readonly ILaunchMode launchMode;
        private readonly DecentralandEnvironment environment;
        private readonly string? gatekeeperBaseOverride;
        private readonly string? optimizedAssetsBaseOverride;
        private readonly bool isTodayEnvironment;

        /// <summary>
        ///     The domain <see cref="RawUrl" /> composes every host from. Written only by the constructor — the today
        ///     environment resolves the handful of hosts it serves from <c>.today</c> and then moves to org for
        ///     everything resolved afterwards, which is why urls must stay lazily resolved — so it is settled by the
        ///     time the instance is handed out.
        /// </summary>
        public string BaseDomain { get; private set; }

        public DecentralandUrlsSource(
            DecentralandEnvironment environment,
            IRealmData realmData,
            ILaunchMode launchMode,
            GatekeeperMode gatekeeperMode = GatekeeperMode.Org,
            string customGatekeeperUrl = "",
            string? cliGatekeeperUrl = null,
            string? cliOptimizedAssetsUrl = null,
            string? customBaseDomain = null)
        {
            this.environment = environment;
            BaseDomain = ResolveBaseDomain(environment, customBaseDomain);
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
                BaseDomain = IDecentralandUrlsSource.ORG_DOMAIN;
            }

            realmData.RealmType.OnUpdate += ResetRealmDependentUrls;
        }

        /// <summary>
        ///     The single place the client's base domain is decided. <see cref="DecentralandEnvironment.Custom" /> is
        ///     the only environment whose domain is not derived from its own name, and the only one that accepts
        ///     <paramref name="customBaseDomain" />: pairing a base domain with any other environment is a wiring
        ///     mistake that would leave the client on <c>decentraland.*</c>, so it throws instead of being ignored.
        /// </summary>
        public static string ResolveBaseDomain(DecentralandEnvironment environment, string? customBaseDomain)
        {
            string? normalized = NormalizeBaseDomain(customBaseDomain);

            if (environment == DecentralandEnvironment.Custom)
                return normalized ?? throw new ArgumentException($"{nameof(DecentralandEnvironment)}.{nameof(DecentralandEnvironment.Custom)} requires a base domain", nameof(customBaseDomain));

            if (normalized != null)
                throw new ArgumentException($"A custom base domain ('{normalized}') only applies to {nameof(DecentralandEnvironment)}.{nameof(DecentralandEnvironment.Custom)}, not {environment}", nameof(customBaseDomain));

            return $"decentraland.{environment.ToString()!.ToLower()}";
        }

        /// <summary>
        ///     Only a bare registrable domain is accepted - no scheme, userinfo, port or path - because the value
        ///     becomes a host-trust suffix. Null / blank means "not supplied".
        /// </summary>
        private static string? NormalizeBaseDomain(string? customBaseDomain)
        {
            if (customBaseDomain?.Trim().Trim('.') is not { Length: > 0 } domain)
                return null;

            if (domain.IndexOfAny(BASE_DOMAIN_FORBIDDEN_CHARS) >= 0)
                throw new ArgumentException($"'{domain}' is not a bare domain: a base domain carries no scheme, userinfo, port or path", nameof(customBaseDomain));

            return domain;
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

        public static DecentralandUrlsSource CreateForTest(string customBaseDomain, ILaunchMode launchMode) =>
            new (DecentralandEnvironment.Custom, new IRealmData.Fake(), launchMode, customBaseDomain: customBaseDomain);

        public string Probe(DecentralandUrl decentralandUrl)
        {
            if (cache.TryGetValue(decentralandUrl, out UrlData cached))
                return cached.Url!;

            return RawUrl(decentralandUrl).ToString();
        }

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
                        // RawUrl already composed the host from BaseDomain, so there is nothing left to substitute.
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
        ///     https://abcdn.{BaseDomain}. FeatureFlagsDependent means it is re-resolved (not cached) until flags load.
        /// </summary>
        private UrlData ResolveOptimizedAssetsUrl(string dedicatedHostUrl)
        {
            if (optimizedAssetsBaseOverride is { Length: > 0 })
                return new UrlData(CacheBehaviour.FeatureFlagsDependent, optimizedAssetsBaseOverride);

            FeatureFlagsConfiguration featureFlags = FeatureFlagsConfiguration.Instance;

            if (featureFlags.IsEmpty)
                return isTodayEnvironment
                    ? dedicatedHostUrl // Static — pinned on construction, before the host domain switches to org
                    : new UrlData(CacheBehaviour.FeatureFlagsDependent, dedicatedHostUrl);

            if (!featureFlags.IsEnabled(FeatureFlagsStrings.OPTIMIZED_ASSETS))
                return dedicatedHostUrl;

            if (featureFlags.TryGetTextPayload(FeatureFlagsStrings.OPTIMIZED_ASSETS, FeatureFlagsStrings.OPTIMIZED_ASSETS_BASE_URL_VARIANT, out string? customBaseUrl) && customBaseUrl is { Length: > 0 })
                return new UrlData(CacheBehaviour.FeatureFlagsDependent, customBaseUrl.TrimEnd('/'));

            return new UrlData(CacheBehaviour.FeatureFlagsDependent, $"https://abcdn.{BaseDomain}");
        }

        /// <summary>Registry-composed endpoints inherit the registry base's caching so a flag-driven base is not cached early.</summary>
        private UrlData ComposeRegistryUrl(string path) =>
            new (RawUrl(DecentralandUrl.AssetBundleRegistry).Caching, $"{Url(DecentralandUrl.AssetBundleRegistry)}{path}");

        /// <summary>
        ///     Composes the feature-flags host before any instance exists (the pre-login whitelist fetch), from the
        ///     same <see cref="ResolveBaseDomain" /> decision the instance uses, so the two cannot diverge.
        /// </summary>
        public static string GetFeatureFlagsUrl(DecentralandEnvironment env, string? customBaseDomain = null) =>
            $"https://{FEATURE_FLAGS_SUBDOMAIN}.{ResolveBaseDomain(env, customBaseDomain)}";

        protected virtual UrlData RawUrl(DecentralandUrl decentralandUrl) =>
            decentralandUrl switch
            {
                DecentralandUrl.SupportLink => $"https://{BaseDomain}/help/",
                DecentralandUrl.DiscordDirectLink => "https://discord.gg/decentraland",
                DecentralandUrl.TwitterLink => "https://x.com/decentraland",
                DecentralandUrl.TwitterNewPostLink => "https://twitter.com/intent/tweet?text={0}&hashtags={1}&url={2}",
                DecentralandUrl.NewsletterSubscriptionLink => "https://decentraland.beehiiv.com/?utm_org=dcl&utm_source=client&utm_medium=organic&utm_campaign=marketplacecredits&utm_term=trialend",
                DecentralandUrl.MarketplaceLink => $"https://{BaseDomain}/marketplace",
                DecentralandUrl.ShopLink => $"https://{BaseDomain}/shop",
                DecentralandUrl.MarketplaceServer => $"https://marketplace-api.{BaseDomain}",
                DecentralandUrl.PrivacyPolicy => $"https://{BaseDomain}/privacy",
                DecentralandUrl.TermsOfUse => $"https://{BaseDomain}/terms",
                DecentralandUrl.ContentPolicy => $"https://{BaseDomain}/content",
                DecentralandUrl.CodeOfEthics => $"https://{BaseDomain}/ethics",
                DecentralandUrl.ApiPlaces => $"https://places.{BaseDomain}/api/places",
                DecentralandUrl.ApiWorlds => $"https://places.{BaseDomain}/api/worlds",
                DecentralandUrl.ApiDestinations => $"https://places.{BaseDomain}/api/destinations",
                DecentralandUrl.ApiAuth => $"https://auth-api.{BaseDomain}",
                DecentralandUrl.ApiRpc => $"wss://rpc.{BaseDomain}",
                DecentralandUrl.MetaTransactionServer => $"https://transactions-api.{BaseDomain}/v1/transactions",
                DecentralandUrl.AuthSignatureWebApp => $"https://{BaseDomain}/auth/requests",
                DecentralandUrl.BuilderApiDtos => $"https://builder-api.{BaseDomain}/v1/collections/[COL-ID]/items",
                DecentralandUrl.BuilderApiContent => $"https://builder-api.{BaseDomain}/v1/storage/contents/",
                DecentralandUrl.BuilderApiNewsletter => $"https://builder-api.{BaseDomain}/v1/newsletter",
                DecentralandUrl.POI => $"https://dcl-lists.{BaseDomain}/pois",
                DecentralandUrl.Map => $"https://places.{BaseDomain}/api/map",
                DecentralandUrl.ContentModerationReport => $"https://places.{BaseDomain}/api/report",
                DecentralandUrl.Gatekeeper => ResolveGatekeeperBaseUrl($"https://comms-gatekeeper.{BaseDomain}"),
                DecentralandUrl.GateKeeperSceneAdapter => $"{RawUrl(DecentralandUrl.Gatekeeper).Url!}{SCENE_ADAPTER_PATH}",
                // The local gatekeeper is pinned to org for the decentraland environments (that is where it runs);
                // a custom deployment runs its own, so it resolves against the base domain instead of reaching for org.
                DecentralandUrl.LocalGateKeeperSceneAdapter => $"{ResolveGatekeeperBaseUrl("https://comms-gatekeeper-local." + (environment == DecentralandEnvironment.Custom ? BaseDomain : IDecentralandUrlsSource.ORG_DOMAIN))}{SCENE_ADAPTER_PATH}",
                DecentralandUrl.ChatAdapter => $"{RawUrl(DecentralandUrl.Gatekeeper).Url!}/private-messages/token",
                DecentralandUrl.ApiEvents => $"https://events.{BaseDomain}/api/events",
                DecentralandUrl.WhatsOnNewEventLink => $"https://{BaseDomain}/whats-on/new-event",
                DecentralandUrl.WhatsOnEventLink => $"https://{BaseDomain}/whats-on/?id={{0}}",
                DecentralandUrl.OpenSea => $"https://opensea.{BaseDomain}",
                DecentralandUrl.Host => $"https://{BaseDomain}",
                DecentralandUrl.ApiChunks => $"https://api.{BaseDomain}/v1/map.png",
                DecentralandUrl.PeerAbout => $"https://peer.{BaseDomain}/about",
                DecentralandUrl.PeerContent => $"https://peer.{BaseDomain}/content/contents",
                DecentralandUrl.RemotePeers => $"https://archipelago-ea-stats.{BaseDomain}/comms/peers",
                DecentralandUrl.RemotePeersWorld => $"https://worlds-content-server.{BaseDomain}/wallet/[USER-ID]/connected-world",
                DecentralandUrl.DAO => $"https://{BaseDomain}/dao/",
                DecentralandUrl.FeatureFlags => $"https://{FEATURE_FLAGS_SUBDOMAIN}.{BaseDomain}",
                DecentralandUrl.Help => $"https://{BaseDomain}/help/",
                DecentralandUrl.Faqs => $"https://docs.{BaseDomain}/faqs/decentraland-101",
                DecentralandUrl.Discord => $"https://{BaseDomain}/discord/",
                DecentralandUrl.Account => $"https://{BaseDomain}/account/",
                DecentralandUrl.MinimumSpecs => $"https://docs.{BaseDomain}/player/FAQs/decentraland-101/#what-hardware-do-i-need-to-run-decentraland",
                DecentralandUrl.Market => $"https://market.{BaseDomain}",
                DecentralandUrl.AssetBundlesCDN => ResolveOptimizedAssetsUrl($"https://ab-cdn.{BaseDomain}"),
                DecentralandUrl.LodGeneratorCDN => ResolveOptimizedAssetsUrl($"https://lod-generator-unity-cdn.{BaseDomain}"),
                DecentralandUrl.ArchipelagoStatus => $"https://archipelago-ea-stats.{BaseDomain}/status",
                DecentralandUrl.ArchipelagoHotScenes => $"https://archipelago-ea-stats.{BaseDomain}/hot-scenes",
                DecentralandUrl.GatekeeperStatus => $"{RawUrl(DecentralandUrl.Gatekeeper).Url!}/status",
                DecentralandUrl.Genesis => $"https://realm-provider-ea.{BaseDomain}/main",
                DecentralandUrl.Badges => $"https://badges.{BaseDomain}",
                DecentralandUrl.CameraReelUsers => $"https://camera-reel-service.{BaseDomain}/api/users",
                DecentralandUrl.CameraReelImages => $"https://camera-reel-service.{BaseDomain}/api/images",
                DecentralandUrl.CameraReelPlaces => $"https://camera-reel-service.{BaseDomain}/api/places",
                DecentralandUrl.CameraReelLink => $"https://reels.{BaseDomain}",
                DecentralandUrl.Blocklist => $"https://config.{BaseDomain}/denylist.json",
                DecentralandUrl.ApiFriends => $"wss://rpc-social-service-ea.{BaseDomain}",
                DecentralandUrl.AssetBundleRegistry => ResolveOptimizedAssetsUrl($"https://asset-bundle-registry.{BaseDomain}"),

                DecentralandUrl.AssetBundleRegistryVersion => ComposeRegistryUrl("/entities/versions"),
                DecentralandUrl.MarketplaceClaimName => $"https://{BaseDomain}/marketplace/names/claim",
                DecentralandUrl.WorldPermissions => $"https://worlds-content-server.{BaseDomain}/world/{{0}}/permissions",
                DecentralandUrl.WorldComms => $"https://worlds-content-server.{BaseDomain}/worlds/{{0}}/comms",
                DecentralandUrl.WorldServer => $"https://worlds-content-server.{BaseDomain}/world",
                DecentralandUrl.WorldContentServer => $"https://worlds-content-server.{BaseDomain}/contents/",
                DecentralandUrl.Servers => $"https://peer.{BaseDomain}/lambdas/contracts/servers",
                DecentralandUrl.MediaConverter => $"https://metamorph-api.{BaseDomain}/convert?url={{0}}",
                DecentralandUrl.MarketplaceCredits => $"https://credits.{BaseDomain}",
                DecentralandUrl.GoShoppingWithMarketplaceCredits => $"https://{BaseDomain}/marketplace/browse?sortBy=newest&status=on_sale&withCredits=true",
                DecentralandUrl.Notifications => $"https://notifications.{BaseDomain}",
                DecentralandUrl.Communities => $"https://social-api.{BaseDomain}/v1/communities",
                DecentralandUrl.CommunitiesV2 => $"https://social-api.{BaseDomain}/v2/communities",
                DecentralandUrl.ReferralProgress => $"https://social-api.{BaseDomain}/v1/referral-progress",
                DecentralandUrl.CommunityThumbnail => $"https://assets-cdn.{BaseDomain}/social/communities/{{0}}/raw-thumbnail.png",
                DecentralandUrl.Members => $"https://social-api.{BaseDomain}/v1/members",
                DecentralandUrl.MembersV2 => $"https://social-api.{BaseDomain}/v2/members",
                DecentralandUrl.CommunityProfileLink => $"https://{BaseDomain}/social/communities/{{0}}?utm_org=dcl&utm_source=explorer&utm_medium=organic&utm_campaign=communities",
                DecentralandUrl.DecentralandWorlds => "https://decentraland.org/blog/about-decentraland/decentraland-worldsTake -your-own-virtual-space?utm_org=dcl&utm_source=explorer&utm_medium=organic",
                DecentralandUrl.ChatTranslate => $"https://autotranslate-server.{BaseDomain}/translate",
                DecentralandUrl.ActiveCommunityVoiceChats => $"https://social-api.{BaseDomain}/v1/community-voice-chats/active",
                DecentralandUrl.Support => $"https://docs.{BaseDomain}/player/support/",
                DecentralandUrl.CreatorHub => $"https://{BaseDomain}/create/",
                DecentralandUrl.ManaUsdRateApiUrl => "https://api.coingecko.com/api/v3/simple/price?ids=decentraland&vs_currencies=usd",
                DecentralandUrl.JumpInGenesisCityLink => $"https://{BaseDomain}/jump/?position={{0}},{{1}}",
                DecentralandUrl.JumpInWorldLink => $"https://{BaseDomain}/jump/?realm={{0}}",
                DecentralandUrl.ReportUserForm => $"https://{BaseDomain}/report/players?player_address={{0}}&reported_address={{1}}",
                DecentralandUrl.BannedUsers => $"{RawUrl(DecentralandUrl.Gatekeeper).Url!}/users/{{0}}/bans",
                DecentralandUrl.SceneAdmins => $"{RawUrl(DecentralandUrl.Gatekeeper).Url!}/scene-admin",
                DecentralandUrl.Pulse => $"pulse-server.{BaseDomain}",

                DecentralandUrl.Profiles => ComposeRegistryUrl("/profiles"),
                DecentralandUrl.ProfilesMetadata => ComposeRegistryUrl("/profiles/metadata"),
                DecentralandUrl.WorldCommsAdapter => $"https://worlds-content-server.{BaseDomain}/worlds/{{0}}/scenes/{{1}}/comms",

                DecentralandUrl.EntitiesActive => UrlData.RealmDependent(FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.ASSET_BUNDLE_FALLBACK) && launchMode.CurrentMode != LaunchMode.LocalSceneDevelopment ? $"{Url(DecentralandUrl.AssetBundleRegistry)}/entities/active" :
                    realmData.Configured ? realmData.Ipfs.EntitiesActiveEndpoint.Value : null),

                // Meant for Wearables and Emotes since they always must be solved by the AB-Registry
                DecentralandUrl.EntitiesActiveElements => ComposeRegistryUrl("/entities/active"),

                DecentralandUrl.WorldEntitiesActive => UrlData.RealmDependent(FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.ASSET_BUNDLE_FALLBACK) && launchMode.CurrentMode != LaunchMode.LocalSceneDevelopment ? $"{Url(DecentralandUrl.AssetBundleRegistry)}/entities/active?world_name={{0}}" :
                    realmData.Configured ? realmData.Ipfs.EntitiesActiveEndpoint.Value : null),

                DecentralandUrl.EntitiesDeployment => UrlData.RealmDependent(realmData.Configured ? realmData.Ipfs.EntitiesBaseUrl.Value : null),
                DecentralandUrl.Lambdas => UrlData.RealmDependent(realmData.Configured ? realmData.Ipfs.LambdasBaseUrl.Value : null),
                DecentralandUrl.Content => UrlData.RealmDependent(realmData.Configured ? realmData.Ipfs.ContentBaseUrl.Value : null),

                DecentralandUrl.SocialServiceMutes => $"https://social-api.{BaseDomain}/v1/mutes",

                // The per-environment proxy only accepts requests whose Origin header matches its environment's web client origin.
                DecentralandUrl.IntercomTickets => $"https://intercom-proxy.{BaseDomain}/intercom/tickets",
                DecentralandUrl.IntercomTicketsOrigin => $"https://play.{BaseDomain}",

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
