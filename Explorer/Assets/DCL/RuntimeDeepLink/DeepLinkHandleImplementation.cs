using Cysharp.Threading.Tasks;
using DCL.RealmNavigation;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using System.Threading;
using UnityEngine;

namespace DCL.RuntimeDeepLink
{
    public class DeepLinkHandleImplementation
    {
        private readonly StartParcel startParcel;
        private readonly IRealmNavigator realmNavigator;
        private readonly CancellationToken token;

        public DeepLinkHandleImplementation(StartParcel startParcel, IRealmNavigator realmNavigator, CancellationToken token)
        {
            this.startParcel = startParcel;
            this.realmNavigator = realmNavigator;
            this.token = token;
        }

        public HandleResult HandleDeepLink(DeepLink deeplink)
        {
            Vector2Int? position = PositionFrom(deeplink);

            if (position.HasValue)
            {
                var parcel = position.Value;

                if (startParcel.IsConsumed())
                    realmNavigator.TeleportToParcelAsync(position.Value, token, false).Forget();
                else
                    startParcel.Assign(parcel);

                return HandleResult.Ok();
            }

            return HandleResult.FromHandleError(new HandleError("no matches"));
        }

        private static Vector2Int? PositionFrom(DeepLink deeplink)
        {
            string? rawPosition = deeplink.ValueOf(AppArgsFlags.POSITION);
            string[]? parts = rawPosition?.Split(',');

            if (parts == null || parts.Length < 2)
                return null;

            if (int.TryParse(parts[0], out int x) == false) return null;
            if (int.TryParse(parts[1], out int y) == false) return null;

            return new Vector2Int(x, y);
        }
    }
}
