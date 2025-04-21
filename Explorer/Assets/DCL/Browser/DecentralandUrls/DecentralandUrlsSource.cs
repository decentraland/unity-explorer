using DCL.Multiplayer.Connections.DecentralandUrls;
using Global.Dynamic.LaunchModes;
using System;
using System.Collections.Generic;

namespace DCL.Browser.DecentralandUrls
{
    //TODO test urls
    public class DecentralandUrlsSource : IDecentralandUrlsSource
    {
        private const string ENV = "{ENV}";
        private static string ASSET_BUNDLE_URL;
        private static string GENESIS_URL;

        private const string ASSET_BUNDLE_URL_TEMPLATE = "https://ab-cdn.decentraland.{0}";
        private const string GENESIS_URL_TEMPLATE = "https://realm-provider-ea.decentraland.{0}/main";


        private readonly Dictionary<DecentralandUrl, string> cache = new ();
        private readonly ILaunchMode launchMode;

        public string DecentralandDomain { get; }
        public DecentralandEnvironment Environment { get; }

        public DecentralandUrlsSource(DecentralandEnvironment environment, ILaunchMode launchMode)
        {
            Environment = environment;
            DecentralandDomain = environment.ToString()!.ToLower();
            this.launchMode = launchMode;

            switch (environment)
            {
                case DecentralandEnvironment.Org:
                case DecentralandEnvironment.Zone:
                    ASSET_BUNDLE_URL = string.Format(ASSET_BUNDLE_URL_TEMPLATE, DecentralandDomain);
                    GENESIS_URL = string.Format(GENESIS_URL_TEMPLATE, DecentralandDomain);
                    break;
                case DecentralandEnvironment.Today:

                    //The today environemnt is a mixture of the org and today enviroments.
                    //We want to fetch pointers from org, but asset bundles from today
                    //Thats because how peer-testing.decentraland.org works.
                    //Its a catalyst that replicates the org environment and eth network, but doesnt propagate back to the production catalysts
                    DecentralandDomain = DecentralandEnvironment.Org.ToString()!.ToLower();
                    ASSET_BUNDLE_URL = "https://ab-cdn.decentraland.today";

                    //On staging, we hardcode the catalyst because its the only valid one with a valid comms configuration
                    GENESIS_URL = "https://peer-testing.decentraland.org";
                    break;
            }
        }

        public string Url(DecentralandUrl decentralandUrl)
        {
            if (cache.TryGetValue(decentralandUrl, out string? url) == false)
            {
                url = RawUrl(decentralandUrl).Replace(ENV, DecentralandDomain);
                cache[decentralandUrl] = url;
            }

            return url!;
        }

        public string GetHostnameForFeatureFlag() =>
            launchMode.CurrentMode switch
            {
                LaunchMode.Play => Url(DecentralandUrl.Host),
                LaunchMode.LocalSceneDevelopment => "localhost", //TODO should this behaviour be extracted to Url() call?
                _ => throw new ArgumentOutOfRangeException()
            };

        private static string RawUrl(DecentralandUrl decentralandUrl) =>
            decentralandUrl switch
            {
                DecentralandUrl.DiscordLink => $"https://decentraland.{ENV}/discord/",
                DecentralandUrl.TwitterLink => "https://x.com/decentraland",
                DecentralandUrl.NewsletterSubscriptionLink => "https://decentraland.beehiiv.com/?utm_org=dcl&utm_source=client&utm_medium=organic&utm_campaign=marketplacecredits&utm_term=trialend",
                DecentralandUrl.MarketplaceLink => $"https://decentraland.{ENV}/marketplace",
                DecentralandUrl.PrivacyPolicy => $"https://decentraland.{ENV}/privacy",
                DecentralandUrl.TermsOfUse => $"https://decentraland.{ENV}/terms",
                DecentralandUrl.ApiPlaces => $"https://places.decentraland.{ENV}/api/places",
                DecentralandUrl.ApiAuth => $"https://auth-api.decentraland.{ENV}",
                DecentralandUrl.ApiRpc => $"wss://rpc.decentraland.{ENV}",
                DecentralandUrl.AuthSignatureWebApp => $"https://decentraland.{ENV}/auth/requests",
                DecentralandUrl.BuilderApiDtos => $"https://builder-api.decentraland.{ENV}/v1/collections/[COL-ID]/items",
                DecentralandUrl.BuilderApiContent => $"https://builder-api.decentraland.{ENV}/v1/storage/contents/",
                DecentralandUrl.POI => $"https://dcl-lists.decentraland.{ENV}/pois",
                DecentralandUrl.Map => $"https://places.decentraland.{ENV}/api/map",
                DecentralandUrl.ContentModerationReport => $"https://places.decentraland.{ENV}/api/report",
                DecentralandUrl.GateKeeperSceneAdapter => $"https://comms-gatekeeper.decentraland.{ENV}/get-scene-adapter",
                DecentralandUrl.LocalGateKeeperSceneAdapter => "https://comms-gatekeeper-local.decentraland.org/get-scene-adapter",
                DecentralandUrl.ApiEvents => $"https://events.decentraland.{ENV}/api/events",
                DecentralandUrl.OpenSea => $"https://opensea.decentraland.{ENV}",
                DecentralandUrl.Host => $"https://decentraland.{ENV}",
                DecentralandUrl.ApiChunks => $"https://api.decentraland.{ENV}/v1/map.png",
                DecentralandUrl.PeerAbout => $"https://peer.decentraland.{ENV}/about",
                DecentralandUrl.RemotePeers => $"https://archipelago-ea-stats.decentraland.{ENV}/comms/peers",
                DecentralandUrl.RemotePeersWorld => $"https://worlds-content-server.decentraland.org/wallet/[USER-ID]/connected-world",
                DecentralandUrl.DAO => $"https://decentraland.{ENV}/dao/",
                DecentralandUrl.Notification => $"https://notifications.decentraland.{ENV}/notifications",
                DecentralandUrl.NotificationRead => $"https://notifications.decentraland.{ENV}/notifications/read",
                DecentralandUrl.FeatureFlags => $"https://feature-flags.decentraland.{ENV}",
                DecentralandUrl.Help => $"https://decentraland.{ENV}/help/",
                DecentralandUrl.MinimumSpecs => $"https://docs.decentraland.{ENV}/player/FAQs/decentraland-101/#what-hardware-do-i-need-to-run-decentraland",
                DecentralandUrl.Market => $"https://market.decentraland.{ENV}",
                DecentralandUrl.AssetBundlesCDN => ASSET_BUNDLE_URL,
                DecentralandUrl.ArchipelagoStatus => $"https://archipelago-ea-stats.decentraland.{ENV}/status",
                DecentralandUrl.ArchipelagoHotScenes => $"https://archipelago-ea-stats.decentraland.{ENV}/hot-scenes",
                DecentralandUrl.GatekeeperStatus => $"https://comms-gatekeeper.decentraland.{ENV}/status",
                DecentralandUrl.Genesis => GENESIS_URL,
                DecentralandUrl.Badges => $"https://badges.decentraland.{ENV}",
                DecentralandUrl.CameraReelUsers => $"https://camera-reel-service.decentraland.{ENV}/api/users",
                DecentralandUrl.CameraReelImages => $"https://camera-reel-service.decentraland.{ENV}/api/images",
                DecentralandUrl.CameraReelPlaces => $"https://camera-reel-service.decentraland.{ENV}/api/places",
                DecentralandUrl.CameraReelLink => $"https://reels.decentraland.{ENV}",
                DecentralandUrl.Blocklist => $"https://config.decentraland.{ENV}/denylist.json",
                DecentralandUrl.ApiFriends => $"wss://rpc-social-service-ea.decentraland.{ENV}",
                DecentralandUrl.AssetBundleRegistry => $"https://asset-bundle-registry.decentraland.{ENV}/entities/active",
                DecentralandUrl.MarketplaceClaimName => $"https://decentraland.{ENV}/marketplace/names/claim",
                DecentralandUrl.WorldContentServer => $"https://worlds-content-server.decentraland.{ENV}/world",
                DecentralandUrl.Servers => $"https://peer.decentraland.{ENV}/lambdas/contracts/servers",
                DecentralandUrl.MediaConverter => $"https://media-opticonverter.decentraland.{ENV}/convert?ktx2=true&fileUrl={{0}}",
                DecentralandUrl.MarketplaceCredits => $"https://credits.decentraland.{ENV}",
                DecentralandUrl.EmailSubscriptions => $"https://notifications.decentraland.{ENV}",
                _ => throw new ArgumentOutOfRangeException(nameof(decentralandUrl), decentralandUrl, null!)
            };
    }
}
