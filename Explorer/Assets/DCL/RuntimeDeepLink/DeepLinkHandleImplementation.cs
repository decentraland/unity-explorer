using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.Communities;
using DCL.ExplorePanel;
using DCL.Diagnostics;
using DCL.RealmNavigation;
using DCL.Utilities;
using Global.AppArgs;
using MVC;
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
        private readonly IMVCManager mvcManager;
        private readonly ILoadingStatus loadingStatus;
        private readonly ReactiveProperty<string?> deeplinkSigninIdentityId;
        private readonly IReadonlyReactiveProperty<string?> loginAwaitingSigninRequestId;
        private readonly bool routeNavigationDeepLinks;

        public DeepLinkHandle(StartParcel startParcel, ChatTeleporter chatTeleporter, CancellationToken token, CommunityDataService communityDataService, IMVCManager mvcManager, ILoadingStatus loadingStatus, ReactiveProperty<string?> deeplinkSigninIdentityId,
            IReadonlyReactiveProperty<string?> loginAwaitingSigninRequestId, bool routeNavigationDeepLinks)
        {
            this.startParcel = startParcel;
            this.chatTeleporter = chatTeleporter;
            this.token = token;
            this.communityDataService = communityDataService;
            this.mvcManager = mvcManager;
            this.loadingStatus = loadingStatus;
            this.deeplinkSigninIdentityId = deeplinkSigninIdentityId;
            this.loginAwaitingSigninRequestId = loginAwaitingSigninRequestId;
            this.routeNavigationDeepLinks = routeNavigationDeepLinks;
        }

        public DeepLinkHandleResult HandleDeepLink(DeepLink deeplink)
        {
            string? signin = deeplink.ValueOf(AppArgsFlags.SIGNIN);

            if (!string.IsNullOrEmpty(signin))
            {
                string? awaitedRequestId = loginAwaitingSigninRequestId.Value;

                // Guard: only consume a signin while a login here is waiting for one, and only if the link
                // was minted for that login.
                if (string.IsNullOrEmpty(awaitedRequestId) || deeplink.ValueOf(AppArgsFlags.AUTH_REQUEST_ID) != awaitedRequestId)
                    return DeepLinkHandleResult.DEFERRED;

                // The id persists in the property until it is overwritten or cleared.
                deeplinkSigninIdentityId.Value = signin;
                return DeepLinkHandleResult.CONSUMED;
            }

            if (!routeNavigationDeepLinks)
            {
                ReportHub.Log(ReportCategory.RUNTIME_DEEPLINKS, $"navigation deep link routing is disabled, dropping: {deeplink}");
                return DeepLinkHandleResult.CONSUMED;
            }

            Vector2Int? position = PositionFrom(deeplink);
            URLDomain? realm = RealmFrom(deeplink);
            string? communityId = CommunityFrom(deeplink);
            bool landOnParcel = LandOnParcelFrom(deeplink);

            var handled = false;

            if (realm.HasValue)
            {
                if(position.HasValue)
                    chatTeleporter.TeleportToRealmAsync(realm.Value.Value, position.Value, token, landOnParcel).Forget();
                else
                    chatTeleporter.TeleportToRealmAsync(realm.Value.Value, token).Forget();

                handled = true;
            }
            else if (position.HasValue)
            {
                var parcel = position.Value;

                if (startParcel.IsConsumed())
                    chatTeleporter.TeleportToParcelAsync(position.Value, false, token, landOnParcel).Forget();
                else
                    startParcel.Assign(parcel, landOnParcel);

                handled = true;
            }

            if (!string.IsNullOrEmpty(communityId))
            {
                communityDataService.ShowCommunityDeepLinkNotification(communityId);
                handled = true;
            }

            if (deeplink.ValueOf(AppArgsFlags.FORCE_OPEN_BACKPACK) != null)
            {
                BackpackDeepLinkOpener.OpenBackpackWhenLandedAsync(mvcManager, loadingStatus, token).Forget();
                handled = true;
            }

            return handled ? DeepLinkHandleResult.CONSUMED : DeepLinkHandleResult.NO_MATCHES;
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

        private static bool LandOnParcelFrom(DeepLink deepLink) =>
            string.Equals(deepLink.ValueOf(AppArgsFlags.LAND_ON_PARCEL), "true", System.StringComparison.OrdinalIgnoreCase);
    }
}
