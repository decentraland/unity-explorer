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
        private readonly IReadonlyReactiveProperty<string?> loginAwaitingSigninRequestId;
        private readonly bool routeNavigationDeepLinks;

        public DeepLinkHandle(StartParcel startParcel, ChatTeleporter chatTeleporter, CancellationToken token, CommunityDataService communityDataService, ReactiveProperty<string?> deeplinkSigninIdentityId,
            IReadonlyReactiveProperty<string?> loginAwaitingSigninRequestId, bool routeNavigationDeepLinks)
        {
            this.startParcel = startParcel;
            this.chatTeleporter = chatTeleporter;
            this.token = token;
            this.communityDataService = communityDataService;
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

            Vector2Int? position = deeplink.Position();
            URLDomain? realm = deeplink.Realm();
            string? communityId = deeplink.Community();
            string? spawnPointName = deeplink.SpawnPoint();

            var handled = false;

            if (realm.HasValue)
            {
                if(position.HasValue)
                    chatTeleporter.TeleportToRealmAsync(realm.Value.Value, position.Value, token, spawnPointName).Forget();
                else
                    chatTeleporter.TeleportToRealmAsync(realm.Value.Value, token, spawnPointName).Forget();

                handled = true;
            }
            else if (position.HasValue)
            {
                var parcel = position.Value;

                if (startParcel.IsConsumed())
                    chatTeleporter.TeleportToParcelAsync(position.Value, false, token, spawnPointName).Forget();
                else
                    startParcel.Assign(parcel, spawnPointName);

                handled = true;
            }

            if (!string.IsNullOrEmpty(communityId))
            {
                communityDataService.ShowCommunityDeepLinkNotification(communityId);
                handled = true;
            }

            return handled ? DeepLinkHandleResult.CONSUMED : DeepLinkHandleResult.NO_MATCHES;
        }
    }
}
