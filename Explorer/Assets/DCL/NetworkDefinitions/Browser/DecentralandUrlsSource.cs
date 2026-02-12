using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility;
using ECS;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Pool;

namespace DCL.Browser.DecentralandUrls
{
    //TODO test urls
    public class DecentralandUrlsSource : IDecentralandUrlsSource
    {
        private const string ENV = "{ENV}";

        private readonly Dictionary<DecentralandUrl, UrlData> cache = new ();
        private readonly DecentralandEnvironment environment;
        private readonly IRealmData realmData;
        private readonly ILaunchMode launchMode;
        private readonly string decentralandDomain;

        public DecentralandUrlsSource(DecentralandEnvironment environment, IRealmData realmData, ILaunchMode launchMode)
        {
            decentralandDomain = environment.ToString()!.ToLower();
            this.environment = environment;
            this.realmData = realmData;
            this.launchMode = launchMode;

            if (environment == DecentralandEnvironment.Today)
            {
                // The today environment is a mixture of the org and today environments.
                // Asset delivery (registry and S3) are used with the `.today` extension
                // Content and lambdas url are hardcoded to a particular catalyst
                // Adapter info (both scene and room) also have to responde to the `.today` environment
                // Archipelago status as well, to have a clear minimap
                // All the remaining urls should use the `Org` domain, that's why we change the domain to forcefully `.org`
                // It's a catalyst that replicates the org environment and eth network, but doesn't propagate back to the production catalysts
                Url(DecentralandUrl.AssetBundleRegistry);
                Url(DecentralandUrl.AssetBundlesCDN);
                Url(DecentralandUrl.ArchipelagoStatus);
                Url(DecentralandUrl.ArchipelagoHotScenes);
                Url(DecentralandUrl.Genesis);
                Url(DecentralandUrl.GatekeeperStatus);
                Url(DecentralandUrl.GateKeeperSceneAdapter);
                Url(DecentralandUrl.RemotePeers);
                decentralandDomain = nameof(DecentralandEnvironment.Org).ToLower();
            }

            realmData.RealmType.OnUpdate += ResetRealmDependentUrls;
        }

        /// <summary>
        ///     Creates a fully irrelevant stub
        /// </summary>
        public static DecentralandUrlsSource CreateForTest() =>
            new (DecentralandEnvironment.Zone, new IRealmData.Fake(), ILaunchMode.PLAY);

        public static DecentralandUrlsSource CreateForTest(DecentralandEnvironment environment, ILaunchMode launchMode) =>
            new (environment, new IRealmData.Fake(), launchMode);

        public string Url(DecentralandUrl decentralandUrl)
        {
            const string REALM_DEPENDENT = "<REALM_DEPENDENT>";

            if (!cache.TryGetValue(decentralandUrl, out UrlData urlData))
            {
                urlData = RawUrl(decentralandUrl);

                if (urlData.Caching == CacheBehaviour.REALM_DEPENDENT && !realmData.Configured)
                    return REALM_DEPENDENT;

                urlData = new UrlData(urlData.Caching, urlData.Url!.Replace(ENV, decentralandDomain));

                cache[decentralandUrl] = urlData;
            }

            return urlData.Url!;
        }

        public string GetHostnameForFeatureFlag() =>
            launchMode.CurrentMode switch
            {
                LaunchMode.Play => Url(DecentralandUrl.Host),
                LaunchMode.LocalSceneDevelopment => "localhost", //TODO should this behaviour be extracted to Url() call?
                _ => throw new ArgumentOutOfRangeException(),
            };

        private void ResetRealmDependentUrls(RealmKind __)
        {
            using PooledObject<List<DecentralandUrl>> _ = ListPool<DecentralandUrl>.Get(out List<DecentralandUrl>? realmDependentCachedUrls);

            realmDependentCachedUrls.AddRange(cache.Where(kvp => kvp.Value.Caching == CacheBehaviour.REALM_DEPENDENT).Select(kvp => kvp.Key));

            foreach (DecentralandUrl url in realmDependentCachedUrls)
                cache.Remove(url);
        }

        private UrlData RawUrl(DecentralandUrl decentralandUrl) =>
            decentralandUrl switch
            {
                DecentralandUrl.SupportLink => $"https://decentraland.{ENV}/help/",
                DecentralandUrl.DiscordDirectLink => "https://discord.gg/decentraland",
                DecentralandUrl.TwitterLink => "https://x.com/decentraland",
                DecentralandUrl.TwitterNewPostLink => "https://twitter.com/intent/tweet?text={0}&hashtags={1}&url={2}",
                DecentralandUrl.NewsletterSubscriptionLink => "https://decentraland.beehiiv.com/?utm_org=dcl&utm_source=client&utm_medium=organic&utm_campaign=marketplacecredits&utm_term=trialend",
                DecentralandUrl.MarketplaceLink => $"https://decentraland.{ENV}/marketplace",
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
                DecentralandUrl.GateKeeperSceneAdapter => $"https://comms-gatekeeper.decentraland.{ENV}/get-scene-adapter",
                DecentralandUrl.LocalGateKeeperSceneAdapter => "https://comms-gatekeeper-local.decentraland.org/get-scene-adapter",
                DecentralandUrl.ChatAdapter => $"https://comms-gatekeeper.decentraland.{ENV}/private-messages/token",
                DecentralandUrl.ApiEvents => $"https://events.decentraland.{ENV}/api/events",
                DecentralandUrl.EventsWebpage => $"https://decentraland.{ENV}/events",
                DecentralandUrl.OpenSea => $"https://opensea.decentraland.{ENV}",
                DecentralandUrl.Host => $"https://decentraland.{ENV}",
                DecentralandUrl.ApiChunks => $"https://api.decentraland.{ENV}/v1/map.png",
                DecentralandUrl.PeerAbout => $"https://peer.decentraland.{ENV}/about",
                DecentralandUrl.RemotePeers => $"https://archipelago-ea-stats.decentraland.{ENV}/comms/peers",
                DecentralandUrl.RemotePeersWorld => "https://worlds-content-server.decentraland.org/wallet/[USER-ID]/connected-world",
                DecentralandUrl.DAO => $"https://decentraland.{ENV}/dao/",
                DecentralandUrl.FeatureFlags => $"https://feature-flags.decentraland.{ENV}",
                DecentralandUrl.Help => $"https://decentraland.{ENV}/help/",
                DecentralandUrl.Account => $"https://decentraland.{ENV}/account/",
                DecentralandUrl.MinimumSpecs => $"https://docs.decentraland.{ENV}/player/FAQs/decentraland-101/#what-hardware-do-i-need-to-run-decentraland",
                DecentralandUrl.Market => $"https://market.decentraland.{ENV}",
                DecentralandUrl.AssetBundlesCDN => $"https://ab-cdn.decentraland.{ENV}",
                DecentralandUrl.ArchipelagoStatus => $"https://archipelago-ea-stats.decentraland.{ENV}/status",
                DecentralandUrl.ArchipelagoHotScenes => $"https://archipelago-ea-stats.decentraland.{ENV}/hot-scenes",
                DecentralandUrl.GatekeeperStatus => $"https://comms-gatekeeper.decentraland.{ENV}/status",
                DecentralandUrl.Genesis => $"https://realm-provider-ea.decentraland.{ENV}/main",
                DecentralandUrl.Badges => $"https://badges.decentraland.{ENV}",
                DecentralandUrl.CameraReelUsers => $"https://camera-reel-service.decentraland.{ENV}/api/users",
                DecentralandUrl.CameraReelImages => $"https://camera-reel-service.decentraland.{ENV}/api/images",
                DecentralandUrl.CameraReelPlaces => $"https://camera-reel-service.decentraland.{ENV}/api/places",
                DecentralandUrl.CameraReelLink => $"https://reels.decentraland.{ENV}",
                DecentralandUrl.Blocklist => $"https://config.decentraland.{ENV}/denylist.json",
                DecentralandUrl.ApiFriends => $"wss://rpc-social-service-ea.decentraland.{ENV}",
                DecentralandUrl.AssetBundleRegistry => $"https://asset-bundle-registry.decentraland.{ENV}",
                DecentralandUrl.AssetBundleRegistryVersion => $"{RawUrl(DecentralandUrl.AssetBundleRegistry)}/entities/versions",
                DecentralandUrl.MarketplaceClaimName => $"https://decentraland.{ENV}/marketplace/names/claim",
                DecentralandUrl.WorldServer => $"https://worlds-content-server.decentraland.{ENV}/world",
                DecentralandUrl.WorldContentServer => $"https://worlds-content-server.decentraland.{ENV}/contents/",
                DecentralandUrl.Servers => $"https://peer.decentraland.{ENV}/lambdas/contracts/servers",
                DecentralandUrl.MediaConverter => $"https://metamorph-api.decentraland.{ENV}/convert?url={{0}}",
                DecentralandUrl.MarketplaceCredits => $"https://credits.decentraland.{ENV}",
                DecentralandUrl.GoShoppingWithMarketplaceCredits => $"https://decentraland.{ENV}/marketplace/browse?sortBy=newest&status=on_sale&withCredits=true",
                DecentralandUrl.Notifications => $"https://notifications.decentraland.{ENV}",
                DecentralandUrl.Communities => $"https://social-api.decentraland.{ENV}/v1/communities",
                DecentralandUrl.CommunityThumbnail => $"https://assets-cdn.decentraland.{ENV}/social/communities/{{0}}/raw-thumbnail.png",
                DecentralandUrl.Members => $"https://social-api.decentraland.{ENV}/v1/members",
                DecentralandUrl.CommunityProfileLink => $"https://decentraland.{ENV}/social/communities/{{0}}?utm_org=dcl&utm_source=explorer&utm_medium=organic&utm_campaign=communities",
                DecentralandUrl.DecentralandWorlds => "https://decentraland.org/blog/about-decentraland/decentraland-worldsTake -your-own-virtual-space?utm_org=dcl&utm_source=explorer&utm_medium=organic",
                DecentralandUrl.ChatTranslate => $"https://autotranslate-server.decentraland.{ENV}/translate",
                DecentralandUrl.ActiveCommunityVoiceChats => $"https://social-api.decentraland.{ENV}/v1/community-voice-chats/active",
                DecentralandUrl.Support => $"https://docs.decentraland.{ENV}/player/support/",
                DecentralandUrl.CreatorHub => $"https://decentraland.{ENV}/create/",
                DecentralandUrl.ManaUsdRateApiUrl => $"https://api.coingecko.com/api/v3/simple/price?ids=decentraland&vs_currencies=usd",
                DecentralandUrl.JumpInGenesisCityLink => $"https://decentraland.{ENV}/jump/?position={{0}},{{1}}",
                DecentralandUrl.JumpInWorldLink => $"https://decentraland.{ENV}/jump/?realm={{0}}",

                DecentralandUrl.Profiles => $"{RawUrl(DecentralandUrl.AssetBundleRegistry)}/profiles",
                DecentralandUrl.ProfilesMetadata => $"{RawUrl(DecentralandUrl.AssetBundleRegistry)}/profiles/metadata",

                DecentralandUrl.EntitiesActive => UrlData.RealmDependent(FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.ASSET_BUNDLE_FALLBACK) && launchMode.CurrentMode != LaunchMode.LocalSceneDevelopment ? $"{RawUrl(DecentralandUrl.AssetBundleRegistry)}/entities/active" :
                    realmData.Configured ? realmData.Ipfs.EntitiesActiveEndpoint.Value : null),

                DecentralandUrl.WorldEntitiesActive => UrlData.RealmDependent(FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.ASSET_BUNDLE_FALLBACK) && launchMode.CurrentMode != LaunchMode.LocalSceneDevelopment ? $"{RawUrl(DecentralandUrl.AssetBundleRegistry)}/entities/active?world_name={{0}}" :
                    realmData.Configured ? realmData.Ipfs.EntitiesActiveEndpoint.Value : null),

                DecentralandUrl.EntitiesDeployment => UrlData.RealmDependent(realmData.Configured ? realmData.Ipfs.EntitiesBaseUrl.Value : null),
                DecentralandUrl.Lambdas => UrlData.RealmDependent(environment == DecentralandEnvironment.Today ? "https://peer-testing.decentraland.org/lambdas/" :
                    realmData.Configured ? realmData.Ipfs.LambdasBaseUrl.Value : null),
                DecentralandUrl.Content => UrlData.RealmDependent(environment == DecentralandEnvironment.Today ? "https://peer-testing.decentraland.org/content/" :
                    realmData.Configured ? realmData.Ipfs.ContentBaseUrl.Value : null),
                _ => throw new ArgumentOutOfRangeException(nameof(decentralandUrl), decentralandUrl, null!),
            };

        private readonly struct UrlData
        {
            public readonly CacheBehaviour Caching;
            public readonly string? Url;

            public UrlData(CacheBehaviour caching, string? url)
            {
                Caching = caching;
                Url = url;
            }

            public static UrlData RealmDependent(string? url) =>
                new (CacheBehaviour.REALM_DEPENDENT, url);

            public static implicit operator UrlData(string rawUrl) =>
                new (CacheBehaviour.STATIC, rawUrl);

            public override string ToString() =>
                Url ?? "<NOT_CONFIGURED>";
        }

        private enum CacheBehaviour
        {
            /// <summary>
            ///     URL is static and can be safely cached
            /// </summary>
            STATIC = 0,

            /// <summary>
            ///     URL should be invalidated upon realm change
            /// </summary>
            REALM_DEPENDENT = 1,
        }
    }
}
