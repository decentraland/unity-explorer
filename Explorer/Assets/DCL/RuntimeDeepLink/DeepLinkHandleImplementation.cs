using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.RealmNavigation;
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
        private readonly IDeeplinkSigninDispatcher deeplinkSigninDispatcher;

        public DeepLinkHandle(StartParcel startParcel, ChatTeleporter chatTeleporter, CancellationToken token, CommunityDataService communityDataService, IDeeplinkSigninDispatcher deeplinkSigninDispatcher)
        {
            this.startParcel = startParcel;
            this.chatTeleporter = chatTeleporter;
            this.token = token;
            this.communityDataService = communityDataService;
            this.deeplinkSigninDispatcher = deeplinkSigninDispatcher;
        }

        public string Name => "Real Implementation";

        public bool HandleDeepLink(DeepLink deeplink)
        {
            // Signin takes precedence over realm/position/community routing and returns before any teleport or
            // community notification is triggered.
            string? signin = deeplink.ValueOf(AppArgsFlags.SIGNIN);

            Debug.Log($"[DLDBG] HandleDeepLink received: {deeplink} | extracted signin='{signin}'"); // TODO: temporary deep-link debug log, remove.

            if (!string.IsNullOrEmpty(signin))
            {
                // Only consume the signin when a login flow is actively awaiting it; otherwise leave the bridge
                // file in place so the instance that is logging in can pick it up.
                if (!deeplinkSigninDispatcher.HasSubscriber)
                {
                    Debug.Log("[DLDBG] HandleDeepLink deferring signin: no subscriber, leaving bridge file"); // TODO: temporary deep-link debug log, remove.
                    return false;
                }

                Debug.Log($"[DLDBG] HandleDeepLink dispatching signin='{signin}'"); // TODO: temporary deep-link debug log, remove.
                deeplinkSigninDispatcher.Dispatch(signin);
                ReportHub.Log(ReportCategory.RUNTIME_DEEPLINKS, $"{Name} dispatched signin deeplink: {deeplink}");
                return true;
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

            if (handled)
                ReportHub.Log(ReportCategory.RUNTIME_DEEPLINKS, $"{Name} successfully handled deeplink: {deeplink}");
            else
                ReportHub.LogWarning(ReportCategory.RUNTIME_DEEPLINKS, $"{Name} found no actionable content in deeplink: {deeplink}");

            // Non-signin deep links are always consumed: nothing awaits them, so leaving the file would re-loop.
            return true;
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
