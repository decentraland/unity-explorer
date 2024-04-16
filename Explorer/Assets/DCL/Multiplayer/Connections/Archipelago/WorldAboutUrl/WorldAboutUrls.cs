using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Archipelago.WorldAboutUrl
{
    public class WorldAboutUrls : IWorldAboutUrls
    {
        private readonly IReadOnlyDictionary<string, string> predefinedUrls = new Dictionary<string, string>
        {
            { "baldr", "https://realm-provider.decentraland.zone/main/about" },
        };

        public string AboutUrl(string realmName) =>
            predefinedUrls.TryGetValue(realmName, out string url)
                ? url!
                : $"https://worlds-content-server.decentraland.org/world/{realmName}/about";
    }
}
