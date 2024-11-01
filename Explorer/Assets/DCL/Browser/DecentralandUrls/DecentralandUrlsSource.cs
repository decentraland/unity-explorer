using DCL.Multiplayer.Connections.DecentralandUrls;
using System;
using System.Collections.Generic;

namespace DCL.Browser.DecentralandUrls
{
    //TODO test urls
    public class DecentralandUrlsSource : IDecentralandUrlsSource
    {
        private static string GENERAL_ENV = "{ENV}";
        private static string ASSET_BUNDLE_ENV = "{ASSET_BUNDLE_ENV}";
        private static string GENESIS_URL; 


        private readonly Dictionary<DecentralandUrl, string> cache = new ();
        private readonly string environmentDomainLowerCase;

        public string DecentralandDomain => environmentDomainLowerCase;

        public DecentralandUrlsSource(DecentralandEnvironment environment)
        {
            environmentDomainLowerCase = environment.ToString()!.ToLower();

            switch (environment)
            {
                case DecentralandEnvironment.Org:
                    GENERAL_ENV = environment.ToString()!.ToLower();
                    ASSET_BUNDLE_ENV = environment.ToString()!.ToLower();
                    GENESIS_URL = "https://realm-provider-ea.decentraland.org/main";
                    break;
                case DecentralandEnvironment.Zone:
                    GENERAL_ENV = environment.ToString()!.ToLower();
                    ASSET_BUNDLE_ENV = environment.ToString()!.ToLower();
                    GENESIS_URL = "https://realm-provider-ea.decentraland.zone/main";
                    break;
                case DecentralandEnvironment.Today:
                    //The today environemnt is a mixture of the org and today enviroments. 
                    //We want to fetch pointers from org, but asset bundles from today
                    //Thats because how peer-testing.decentraland.org works. 
                    //Its a catalyst that replicates the org environment and eth network, but doesnt propagate back to the production catalysts
                    GENERAL_ENV = DecentralandEnvironment.Org.ToString()!.ToLower();
                    ASSET_BUNDLE_ENV = environment.ToString()!.ToLower();
                    //On staging, we hardcode the catalyst because its the only valid one with a valid comms configuration
                    GENESIS_URL = "https://peer-testing.decentraland.org";
                    break;
            }
        }

        public string Url(DecentralandUrl decentralandUrl)
        {
            if (cache.TryGetValue(decentralandUrl, out string? url) == false)
            {
                url = RawUrl(decentralandUrl).Replace(GENERAL_ENV, environmentDomainLowerCase);
                cache[decentralandUrl] = url;
            }

            return url!;
        }

        private static string RawUrl(DecentralandUrl decentralandUrl) =>
            decentralandUrl switch
            {
                DecentralandUrl.DiscordLink => $"https://decentraland.{GENERAL_ENV}/discord/",
                DecentralandUrl.PrivacyPolicy => $"https://decentraland.{GENERAL_ENV}/privacy",
                DecentralandUrl.TermsOfUse => $"https://decentraland.{GENERAL_ENV}/terms",
                DecentralandUrl.ApiPlaces => $"https://places.decentraland.{GENERAL_ENV}/api/places",
                DecentralandUrl.ApiAuth => $"https://auth-api.decentraland.{GENERAL_ENV}",
                DecentralandUrl.AuthSignature => $"https://decentraland.{GENERAL_ENV}/auth/requests",
                DecentralandUrl.POI => $"https://dcl-lists.decentraland.{GENERAL_ENV}/pois",
                DecentralandUrl.ContentModerationReport => $"https://places.decentraland.{GENERAL_ENV}/api/report",
                DecentralandUrl.GateKeeperSceneAdapter =>
                    $"https://comms-gatekeeper.decentraland.{GENERAL_ENV}/get-scene-adapter",
                DecentralandUrl.OpenSea => $"https://opensea.decentraland.{GENERAL_ENV}",
                DecentralandUrl.Host => $"https://decentraland.{GENERAL_ENV}",
                DecentralandUrl.PeerAbout => $"https://peer.decentraland.{GENERAL_ENV}/about",
                DecentralandUrl.DAO => $"https://decentraland.{GENERAL_ENV}/dao/",
                DecentralandUrl.Notification => $"https://notifications.decentraland.{GENERAL_ENV}/notifications",
                DecentralandUrl.NotificationRead =>
                    $"https://notifications.decentraland.{GENERAL_ENV}/notifications/read",
                DecentralandUrl.FeatureFlags => $"https://feature-flags.decentraland.{GENERAL_ENV}",
                DecentralandUrl.Help => $"https://decentraland.{GENERAL_ENV}/help/",
                DecentralandUrl.Market => $"https://market.decentraland.{GENERAL_ENV}",
                DecentralandUrl.AssetBundlesCDN => $"https://ab-cdn.decentraland.{ASSET_BUNDLE_ENV}",
                DecentralandUrl.ArchipelagoStatus => $"https://archipelago-ea-stats.decentraland.{GENERAL_ENV}/status",
                DecentralandUrl.GatekeeperStatus => $"https://comms-gatekeeper.decentraland.{GENERAL_ENV}/status",
                DecentralandUrl.Genesis => GENESIS_URL,
                DecentralandUrl.Badges => $"https://badges.decentraland.{GENERAL_ENV}",
                _ => throw new ArgumentOutOfRangeException(nameof(decentralandUrl), decentralandUrl, null!)
            };
    }
}
