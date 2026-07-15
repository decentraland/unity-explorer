using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.Communities;
using DCL.RealmNavigation;
using DCL.Utility.Types;
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
            Vector2Int? position = deeplink.Position();
            URLDomain? realm = deeplink.Realm();
            string? communityId = deeplink.Community();
            string? spawnPointName = deeplink.SpawnPoint();

            var result = Result.ErrorResult("no matches");

            if (realm.HasValue)
            {
                if(position.HasValue)
                    chatTeleporter.TeleportToRealmAsync(realm.Value.Value, position.Value, token, spawnPointName).Forget();
                else
                    chatTeleporter.TeleportToRealmAsync(realm.Value.Value, token, spawnPointName).Forget();

                result = Result.SuccessResult();
            }
            else if (position.HasValue)
            {
                var parcel = position.Value;

                if (startParcel.IsConsumed())
                    chatTeleporter.TeleportToParcelAsync(position.Value, false, token, spawnPointName).Forget();
                else
                    startParcel.Assign(parcel, spawnPointName);

                result = Result.SuccessResult();
            }

            if (!string.IsNullOrEmpty(communityId))
            {
                communityDataService.ShowCommunityDeepLinkNotification(communityId);
                result = Result.SuccessResult();
            }

            return result;
        }
    }
}
