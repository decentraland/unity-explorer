using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.DecentralandUrls
{
    //TODO test urls
    public class DecentralandUrlsSource : IDecentralandUrlsSource
    {
        private readonly Dictionary<DecentralandUrl, string> cache = new ();
        private readonly DecentralandEnvironment environment;
        private const string ZONE = "{ZONE}";

        public DecentralandUrlsSource(DecentralandEnvironment environment)
        {
            this.environment = environment;
        }

        public string Url(DecentralandUrl decentralandUrl)
        {
            if (cache.TryGetValue(decentralandUrl, out string? url) == false)
            {
                url = RawUrl(decentralandUrl).Replace(ZONE, environment.ToString()!.ToLower());
                cache[decentralandUrl] = url;
            }

            return url!;
        }

        private static string RawUrl(DecentralandUrl decentralandUrl) =>
            decentralandUrl switch
            {
                DecentralandUrl.DiscordLink => $"https://decentraland.{ZONE}/discord/",
                DecentralandUrl.PrivacyPolicy => $"https://decentraland.{ZONE}/privacy",
                DecentralandUrl.TermsOfUse => $"https://decentraland.{ZONE}/terms",
                DecentralandUrl.ApiPlaces => $"https://places.decentraland.{ZONE}/api/places",
                DecentralandUrl.ApiAuth => $"https://auth-api.decentraland.{ZONE}",
                DecentralandUrl.AuthSignature => $"https://decentraland.{ZONE}/auth/requests",
                DecentralandUrl.POI => $"https://dcl-lists.decentraland.{ZONE}/pois",
                DecentralandUrl.ContentModerationReport => $"https://places.decentraland.{ZONE}/api/report",
                _ => throw new ArgumentOutOfRangeException(nameof(decentralandUrl), decentralandUrl, null)
            };
    }
}
