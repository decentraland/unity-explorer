using Cysharp.Threading.Tasks;
using DCL.RealmNavigation;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using System.Threading;
using UnityEngine;
using Utility.Types;

namespace DCL.RuntimeDeepLink
{
    public class DeepLinkHandle : IDeepLinkHandle
    {
        private readonly StartParcel startParcel;
        private readonly IRealmNavigator realmNavigator;
        private readonly CancellationToken token;

        public DeepLinkHandle(StartParcel startParcel, IRealmNavigator realmNavigator, CancellationToken token)
        {
            this.startParcel = startParcel;
            this.realmNavigator = realmNavigator;
            this.token = token;
        }

        public string Name => "Real Implementation";

        public Result HandleDeepLink(DeepLink deeplink)
        {
            Vector2Int? position = PositionFrom(deeplink);

            if (position.HasValue)
            {
                var parcel = position.Value;

                if (startParcel.IsConsumed())
                    realmNavigator.TeleportToParcelAsync(position.Value, token, false).Forget();
                else
                    startParcel.Assign(parcel);

                return Result.SuccessResult();
            }

            return Result.ErrorResult("no matches");
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
