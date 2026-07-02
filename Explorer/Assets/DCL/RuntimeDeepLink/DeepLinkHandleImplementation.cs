using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.RealmNavigation;
using DCL.Utilities;
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
        private readonly ReactiveProperty<string?> deeplinkSigninIdentityId;
        private readonly bool routeNavigationDeepLinks;

        public DeepLinkHandle(StartParcel startParcel, ChatTeleporter chatTeleporter, CancellationToken token, CommunityDataService communityDataService, ReactiveProperty<string?> deeplinkSigninIdentityId,
            bool routeNavigationDeepLinks)
        {
            this.startParcel = startParcel;
            this.chatTeleporter = chatTeleporter;
            this.token = token;
            this.communityDataService = communityDataService;
            this.deeplinkSigninIdentityId = deeplinkSigninIdentityId;
            this.routeNavigationDeepLinks = routeNavigationDeepLinks;
        }

        public DeepLinkHandleResult HandleDeepLink(DeepLink deeplink)
        {
            string? signin = deeplink.ValueOf(AppArgsFlags.SIGNIN);

            if (!string.IsNullOrEmpty(signin))
            {
                // The property retains the id, so a login flow that subscribes later still receives it.
                deeplinkSigninIdentityId.Value = signin;
                return DeepLinkHandleResult.Consumed;
            }

            if (!routeNavigationDeepLinks)
            {
                ReportHub.Log(ReportCategory.RUNTIME_DEEPLINKS, $"navigation deep link routing is disabled, dropping: {deeplink}");
                return DeepLinkHandleResult.Consumed;
            }

            Vector2Int? position = PositionFrom(deeplink);
            URLDomain? realm = RealmFrom(deeplink);
            string? communityId = CommunityFrom(deeplink);

            var handled = false;

            if (realm.HasValue)
            {
                if(position.HasValue)
                    chatTeleporter.TeleportToRealmAsync(realm.Value.Value, position.Value, token).Forget();
                else
                    chatTeleporter.TeleportToRealmAsync(realm.Value.Value, token).Forget();

                handled = true;
            }
            else if (position.HasValue)
            {
                var parcel = position.Value;

                if (startParcel.IsConsumed())
                    chatTeleporter.TeleportToParcelAsync(position.Value, false, token).Forget();
                else
                    startParcel.Assign(parcel);

                handled = true;
            }

            if (!string.IsNullOrEmpty(communityId))
            {
                communityDataService.ShowCommunityDeepLinkNotification(communityId);
                handled = true;
            }

            return handled ? DeepLinkHandleResult.Consumed : DeepLinkHandleResult.NoMatches;
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
