using Cysharp.Threading.Tasks;
using DCL.RealmNavigation;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility.Types;

namespace DCL.RuntimeDeepLink
{
    public class DeepLinkController
    {
        private readonly StartParcel startParcel;
        private readonly IRealmNavigator realmNavigator;
        private readonly CancellationToken token;

        public DeepLinkController(StartParcel startParcel, IRealmNavigator realmNavigator, CancellationToken token)
        {
            this.startParcel = startParcel;
            this.realmNavigator = realmNavigator;
            this.token = token;
        }

        public Result HandleDeepLink(string? deepLinkRawContent)
        {
            if (string.IsNullOrWhiteSpace(deepLinkRawContent!))
                return Result.ErrorResult("Cannot deserialize deeplink content, empty content");

            if (deepLinkRawContent.StartsWith("decentraland://", StringComparison.Ordinal) == false)
                return Result.ErrorResult($"Cannot deserialize deeplink content, wrong format: {deepLinkRawContent}");

            Dictionary<string, string> map = ApplicationParametersParser.ProcessDeepLinkParameters(deepLinkRawContent);

            Vector2Int? position = PositionFrom(map);

            if (position.HasValue)
            {
                var parcel = position.Value;

                if (startParcel.IsConsumed())
                    realmNavigator.TeleportToParcelAsync(position.Value, token, false).Forget();
                else
                    startParcel.Assign(parcel);

                return Result.SuccessResult();
            }

            return Result.ErrorResult("No matches");
        }

        private static Vector2Int? PositionFrom(Dictionary<string, string> deeplinkMap)
        {
            string? rawPosition = deeplinkMap.GetValueOrDefault(AppArgsFlags.POSITION);
            string[]? parts = rawPosition?.Split(',');

            if (parts == null || parts.Length < 2)
                return null;

            if (int.TryParse(parts[0], out int x) == false) return null;
            if (int.TryParse(parts[1], out int y) == false) return null;

            return new Vector2Int(x, y);
        }
    }
}
