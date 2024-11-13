using DCL.Multiplayer.Connections.DecentralandUrls;
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
        private readonly string environmentDomainLowerCase;

        public string DecentralandDomain => environmentDomainLowerCase;

        public DecentralandUrlsSource(DecentralandEnvironment environment)
        {
            environmentDomainLowerCase = environment.ToString()!.ToLower();

            switch (environment)
            {
                case DecentralandEnvironment.Org:
                case DecentralandEnvironment.Zone:
                    ASSET_BUNDLE_URL = string.Format(ASSET_BUNDLE_URL_TEMPLATE, environmentDomainLowerCase);
                    GENESIS_URL = string.Format(GENESIS_URL_TEMPLATE, environmentDomainLowerCase);
                    break;
                case DecentralandEnvironment.Today:

                    //The today environemnt is a mixture of the org and today enviroments.
                    //We want to fetch pointers from org, but asset bundles from today
                    //Thats because how peer-testing.decentraland.org works.
                    //Its a catalyst that replicates the org environment and eth network, but doesnt propagate back to the production catalysts
                    environmentDomainLowerCase = DecentralandEnvironment.Org.ToString()!.ToLower();
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
                url = RawUrl(decentralandUrl).Replace(ENV, environmentDomainLowerCase);
                cache[decentralandUrl] = url;
            }

            return url!;
        }

        private static string RawUrl(DecentralandUrl decentralandUrl) =>
            decentralandUrl switch
            {
                DecentralandUrl.DiscordLink => $"https://decentraland.{ENV}/discord/",
                DecentralandUrl.PrivacyPolicy => $"https://decentraland.{ENV}/privacy",
                DecentralandUrl.TermsOfUse => $"https://decentraland.{ENV}/terms",
                DecentralandUrl.ApiPlaces => $"https://places.decentraland.{ENV}/api/places",
                DecentralandUrl.ApiAuth => $"https://auth-api.decentraland.{ENV}",
                DecentralandUrl.AuthSignature => $"https://decentraland.{ENV}/auth/requests",
                DecentralandUrl.POI => $"https://dcl-lists.decentraland.{ENV}/pois",
                DecentralandUrl.PlacesByCategory => $"https://places.decentraland.{ENV}/api/map",
                DecentralandUrl.ContentModerationReport => $"https://places.decentraland.{ENV}/api/report",
                DecentralandUrl.GateKeeperSceneAdapter => $"https://comms-gatekeeper.decentraland.{ENV}/get-scene-adapter",
                DecentralandUrl.ApiEvents => $"https://events.decentraland.{ENV}/api/events",
                DecentralandUrl.OpenSea => $"https://opensea.decentraland.{ENV}",
                DecentralandUrl.Host => $"https://decentraland.{ENV}",
                DecentralandUrl.ApiChunks => $"https://api.decentraland.{ENV}/v1/map.png",
                DecentralandUrl.PeerAbout => $"https://peer.decentraland.{ENV}/about",
                DecentralandUrl.DAO => $"https://decentraland.{ENV}/dao/",
                DecentralandUrl.Notification => $"https://notifications.decentraland.{ENV}/notifications",
                DecentralandUrl.NotificationRead => $"https://notifications.decentraland.{ENV}/notifications/read",
                DecentralandUrl.FeatureFlags => $"https://feature-flags.decentraland.{ENV}",
                DecentralandUrl.Help => $"https://decentraland.{ENV}/help/",
                DecentralandUrl.Market => $"https://market.decentraland.{ENV}",
                DecentralandUrl.AssetBundlesCDN => ASSET_BUNDLE_URL,
                DecentralandUrl.ArchipelagoStatus => $"https://archipelago-ea-stats.decentraland.{ENV}/status",
                DecentralandUrl.GatekeeperStatus => $"https://comms-gatekeeper.decentraland.{ENV}/status",
                DecentralandUrl.Genesis => GENESIS_URL,
                DecentralandUrl.Badges => $"https://badges.decentraland.{ENV}",
                DecentralandUrl.CameraReelUsers => $"https://camera-reel-service.decentraland.{ENV}/api/users",
                DecentralandUrl.CameraReelImages => $"https://camera-reel-service.decentraland.{ENV}/api/images",
                _ => throw new ArgumentOutOfRangeException(nameof(decentralandUrl), decentralandUrl, null!)
            };
    }
}
