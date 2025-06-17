using Cysharp.Threading.Tasks;
using ECS.SceneLifeCycle.Realm;
using System.Threading;
using UnityEngine;

namespace DCL.RuntimeDeepLink
{
    public class DeepLinkHandleImplementation
    {
        private readonly IRealmNavigator realmNavigator;
        private readonly CancellationToken token;

        public DeepLinkHandleImplementation(IRealmNavigator realmNavigator, CancellationToken token)
        {
            this.realmNavigator = realmNavigator;
            this.token = token;
        }

        public HandleResult HandleDeepLink(DeepLink deeplink)
        {
            Vector2Int? position = PositionFrom(deeplink);

            if (position.HasValue)
            {
                realmNavigator.TeleportToParcelAsync(position.Value, token, false).Forget();
                return HandleResult.Ok();
            }

            return HandleResult.FromHandleError(new HandleError("no matches"));
        }

        private static Vector2Int? PositionFrom(DeepLink deeplink)
        {
            string? rawPosition = deeplink.ValueOf("position");
            string[]? parts = rawPosition?.Split(',');

            if (parts == null || parts.Length < 2)
                return null;

            if (int.TryParse(parts[0], out int x) == false) return null;
            if (int.TryParse(parts[1], out int y) == false) return null;

            return new Vector2Int(x, y);
        }
    }
}
