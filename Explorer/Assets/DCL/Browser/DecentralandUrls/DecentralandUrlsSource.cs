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

        private readonly string contentURLOverride;
        private readonly string lambdasURLOverride;

        private readonly Dictionary<DecentralandUrl, string> cache = new ();
        private readonly ILaunchMode launchMode;
        private readonly string decentralandDomain;

        public DecentralandUrlsSource(DecentralandEnvironment environment, ILaunchMode launchMode)
        {
            decentralandDomain = environment.ToString()!.ToLower();
            this.launchMode = launchMode;

            if (environment == DecentralandEnvironment.Today)
            {
                // The today environment is a mixture of the org and today environments.
                // Asset delivery (registry and S3) are used with the `.today` extension
                // Content and lambdas url are hardcoded to a particular catalyst
                // All the remaining urls should use the `Org` domain, that's why we change the domain to forcefully `.org`
                // It's a catalyst that replicates the org environment and eth network, but doesn't propagate back to the production catalysts
                Url(DecentralandUrl.AssetBundleRegistry);
                Url(DecentralandUrl.AssetBundlesCDN);
                contentURLOverride = "https://peer-testing.decentraland.org/content/";
                lambdasURLOverride = "https://peer-testing.decentraland.org/lambdas/";
                decentralandDomain = nameof(DecentralandEnvironment.Org).ToLower();
            }
            else
            {
                contentURLOverride = "NO_CONTENT_URL_OVERRIDE";
                lambdasURLOverride = "NO_LAMBDAS_URL_OVERRIDE";
            }

        }

        public string Url(DecentralandUrl decentralandUrl)
        {
            if (cache.TryGetValue(decentralandUrl, out string? url) == false)
            {
                url = RawUrl(decentralandUrl).Replace(ENV, decentralandDomain);
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

        private string RawUrl(DecentralandUrl decentralandUrl) =>
            decentralandUrl switch
            {
                DecentralandUrl.DiscordLink => $"https://decentraland.{ENV}/discord/",
                DecentralandUrl.DiscordDirectLink => "https://discord.gg/decentraland",
                DecentralandUrl.TwitterLink => "https://x.com/decentraland",
                DecentralandUrl.NewsletterSubscriptionLink => "https://decentraland.beehiiv.com/?utm_org=dcl&utm_source=client&utm_medium=organic&utm_campaign=marketplacecredits&utm_term=trialend",
                DecentralandUrl.MarketplaceLink => $"https://decentraland.{ENV}/marketplace",
                DecentralandUrl.PrivacyPolicy => $"https://decentraland.{ENV}/privacy",
                DecentralandUrl.TermsOfUse => $"https://decentraland.{ENV}/terms",
                DecentralandUrl.ContentPolicy => $"https://decentraland.{ENV}/content",
                DecentralandUrl.CodeOfEthics => $"https://decentraland.{ENV}/ethics",
                DecentralandUrl.ApiPlaces => $"https://places.decentraland.{ENV}/api/places",
                DecentralandUrl.ApiWorlds => $"https://places.decentraland.{ENV}/api/worlds",
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
                DecentralandUrl.ChatAdapter => $"https://comms-gatekeeper.decentraland.{ENV}/private-messages/token",
                DecentralandUrl.ApiEvents => $"https://events.decentraland.{ENV}/api/events",
                DecentralandUrl.EventsWebpage => $"https://decentraland.{ENV}/events",
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
                DecentralandUrl.MarketplaceClaimName => $"https://decentraland.{ENV}/marketplace/names/claim",
                DecentralandUrl.WorldContentServer => $"https://worlds-content-server.decentraland.{ENV}/world",
                DecentralandUrl.Servers => $"https://peer.decentraland.{ENV}/lambdas/contracts/servers",
                DecentralandUrl.MediaConverter => $"https://metamorph-api.decentraland.{ENV}/convert?url={{0}}",
                DecentralandUrl.MarketplaceCredits => $"https://credits.decentraland.{ENV}",
                DecentralandUrl.GoShoppingWithMarketplaceCredits => $"https://decentraland.{ENV}/marketplace/browse?sortBy=newest&status=on_sale&withCredits=true",
                DecentralandUrl.EmailSubscriptions => $"https://notifications.decentraland.{ENV}",
                DecentralandUrl.Communities => $"https://social-api.decentraland.{ENV}/v1/communities",
                DecentralandUrl.CommunityThumbnail => $"https://assets-cdn.decentraland.{ENV}/social/communities/{{0}}/raw-thumbnail.png",
                DecentralandUrl.Members => $"https://social-api.decentraland.{ENV}/v1/members",
                DecentralandUrl.CommunityProfileLink => $"https://decentraland.{ENV}/social/communities/{{0}}",
                DecentralandUrl.DecentralandWorlds => "https://decentraland.org/blog/about-decentraland/decentraland-worlds-your-own-virtual-space?utm_org=dcl&utm_source=explorer&utm_medium=organic",
                DecentralandUrl.DecentralandLambdasOverride => lambdasURLOverride,
                DecentralandUrl.DecentralandContentOverride => contentURLOverride,
                DecentralandUrl.ChatTranslate => $"https://autotranslate-server.decentraland.{ENV}/translate",
                DecentralandUrl.ActiveCommunityVoiceChats => $"https://social-api.decentraland.{ENV}/v1/community-voice-chats/active",
                _ => throw new ArgumentOutOfRangeException(nameof(decentralandUrl), decentralandUrl, null!)
            };
    }
}
