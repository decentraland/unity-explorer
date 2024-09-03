using DCL.Multiplayer.Connections.DecentralandUrls;
using System;
using System.Collections.Generic;

namespace DCL.Browser.DecentralandUrls
{
    //TODO test urls
    public class DecentralandUrlsSource : IDecentralandUrlsSource
    {
        private const string ENV = "{ENV}";

        private readonly Dictionary<DecentralandUrl, string> cache = new ();
        private readonly string environmentDomainLowerCase;

        public string DecentralandDomain => environmentDomainLowerCase;

        public DecentralandUrlsSource(DecentralandEnvironment environment)
        {
            environmentDomainLowerCase = environment.ToString()!.ToLower();
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
                DecentralandUrl.ContentModerationReport => $"https://places.decentraland.{ENV}/api/report",
                DecentralandUrl.GateKeeperSceneAdapter => $"https://comms-gatekeeper.decentraland.{ENV}/get-scene-adapter",
                DecentralandUrl.OpenSea => $"https://opensea.decentraland.{ENV}",
                DecentralandUrl.Host => $"https://decentraland.{ENV}",
                DecentralandUrl.ApiChunks => $"https://api.decentraland.{ENV}/v1/map.png",
                DecentralandUrl.PeerAbout => $"https://peer.decentraland.{ENV}/about",
                DecentralandUrl.DAO => $"https://decentraland.{ENV}/dao/",
                DecentralandUrl.Notification => $"https://notifications.decentraland.zone/notifications", //TODO (Santi) Replace it by $"https://notifications.decentraland.{ENV}/notifications when Badges back-end is deployed in prod"
                DecentralandUrl.NotificationRead => $"https://notifications.decentraland.zone/notifications/read", // TODO (Santi) Replace it by $"https://notifications.decentraland.{ENV}/notifications/read when Badges back-end is deployed in prod"
                DecentralandUrl.FeatureFlags => $"https://feature-flags.decentraland.{ENV}",
                DecentralandUrl.Market => "https://market.decentraland.org",
                DecentralandUrl.AssetBundlesCDN => "https://ab-cdn.decentraland.org",
                DecentralandUrl.ArchipelagoStatus => $"https://archipelago-stats.decentraland.{ENV}/status",
                DecentralandUrl.GatekeeperStatus => $"https://comms-gatekeeper.decentraland.{ENV}/status",
                DecentralandUrl.Genesis => $"https://realm-provider-ea.decentraland.{ENV}/main",
                DecentralandUrl.Badges => $"https://badges.decentraland.zone", //TODO (Santi) Replace it by $"https://badges.decentraland.{ENV}/badges when Badges back-end is deployed in prod"
                _ => throw new ArgumentOutOfRangeException(nameof(decentralandUrl), decentralandUrl, null!)
            };
    }
}
