using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.Communities;
using DCL.RealmNavigation;
using DCL.Utility.Types;
using Global.AppArgs;
using System.Threading;
using UnityEngine;

namespace DCL.RuntimeDeepLink
{
    public class DeepLinkHandle : IDeepLinkHandle
    {
        private readonly StartParcel startParcel;
        private readonly ChatTeleporter chatTeleporter;
        private readonly CancellationToken token;
        private readonly CommunityDataService communityDataService;

        public DeepLinkHandle(StartParcel startParcel, ChatTeleporter chatTeleporter, CancellationToken token, CommunityDataService communityDataService)
        {
            this.startParcel = startParcel;
            this.chatTeleporter = chatTeleporter;
            this.token = token;
            this.communityDataService = communityDataService;
        }

        public string Name => "Real Implementation";

        public Result HandleDeepLink(DeepLink deeplink)
        {
            Vector2Int? position = PositionFrom(deeplink);
            URLDomain? realm = RealmFrom(deeplink);
            string? communityId = CommunityFrom(deeplink);

            var result = Result.ErrorResult("no matches");

            if (realm.HasValue)
            {
                chatTeleporter.TeleportToRealmAsync(realm.Value.Value, position, token).Forget();
                result = Result.SuccessResult();
            }
            else if (position.HasValue)
            {
                var parcel = position.Value;

                if (startParcel.IsConsumed())
                    chatTeleporter.TeleportToParcelAsync(position.Value, false, token).Forget();
                else
                    startParcel.Assign(parcel);

                result = Result.SuccessResult();
            }

            if (!string.IsNullOrEmpty(communityId))
            {
                communityDataService.ShowCommunityDeepLinkNotification(communityId);
                result = Result.SuccessResult();
            }

            return result;
        }

        private static URLDomain? RealmFrom(DeepLink deepLink)
        {
            string? rawRealm = deepLink.ValueOf(AppArgsFlags.REALM);

            if (rawRealm == null)
                return null;

            return URLDomain.FromString(rawRealm);
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

        private static string? CommunityFrom(DeepLink deepLink)
        {
            string? rawCommunity = deepLink.ValueOf(AppArgsFlags.COMMUNITY);
            return rawCommunity ?? null;
        }
    }
}
