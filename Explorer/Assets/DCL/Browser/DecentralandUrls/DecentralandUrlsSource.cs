using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.DecentralandUrls
{
    //TODO test urls
    public class DecentralandUrlsSource : IDecentralandUrlsSource
    {
        private readonly Dictionary<DecentralandUrl, string> cache = new ();
        private const string ENV = "{ENV}";

        public DecentralandEnvironment Environment { get; }

        public DecentralandUrlsSource(DecentralandEnvironment environment)
        {
            this.Environment = environment;
        }

        public string Url(DecentralandUrl decentralandUrl)
        {
            if (cache.TryGetValue(decentralandUrl, out string? url) == false)
            {
                url = RawUrl(decentralandUrl).Replace(ENV, Environment.ToString()!.ToLower());
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
                _ => throw new ArgumentOutOfRangeException(nameof(decentralandUrl), decentralandUrl, null)
            };
    }
}
