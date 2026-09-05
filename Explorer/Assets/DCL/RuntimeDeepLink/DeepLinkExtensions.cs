using CommunicationData.URLHelpers;
using Global.AppArgs;
using UnityEngine;

namespace DCL.RuntimeDeepLink
{
    public static class DeepLinkExtensions
    {
        public static URLDomain? Realm(this DeepLink deepLink)
        {
            string? rawRealm = deepLink.ValueOf(AppArgsFlags.REALM);

            return rawRealm == null ? null : URLDomain.FromString(rawRealm);
        }

        public static Vector2Int? Position(this DeepLink deepLink)
        {
            string? rawPosition = deepLink.ValueOf(AppArgsFlags.POSITION);
            string[]? parts = rawPosition?.Split(',');

            if (parts == null || parts.Length < 2)
                return null;

            if (int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                return new Vector2Int(x, y);

            return null;
        }

        public static string? SpawnPoint(this DeepLink deepLink)
        {
            string? rawSpawnPoint = deepLink.ValueOf(AppArgsFlags.SPAWN_POINT);
            return string.IsNullOrEmpty(rawSpawnPoint) ? null : rawSpawnPoint;
        }

        public static string? Community(this DeepLink deepLink) =>
            deepLink.ValueOf(AppArgsFlags.COMMUNITY);
    }
}
