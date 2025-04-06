using Cysharp.Threading.Tasks;
using DCL.Friends.UserBlocking;
using DCL.SocialService;
using DCL.Utilities;
using DCL.Web3;
using Decentraland.SocialService.V2;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Chat
{
    public class RPCChatPrivacyService
    {
        private readonly ObjectProxy<ISocialServiceRPC> socialServiceRPCProxy;
        private readonly IChatUserEventBus chatUserEventBus;

        public RPCChatPrivacyService(
            ObjectProxy<ISocialServiceRPC> socialServiceRPCProxy,
            IChatUserEventBus chatUserEventBus)
        {
            this.socialServiceRPCProxy = socialServiceRPCProxy;
            this.chatUserEventBus = chatUserEventBus;
        }

        private const int TIMEOUT_SECONDS = 30;


        public async UniTask UpsertSocialSettingsAsync(bool receiveAllMessages, CancellationToken ct)
        {
            await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);

            var payload = new UpsertSocialSettingsPayload
            {
                PrivateMessagesPrivacy = receiveAllMessages? PrivateMessagePrivacySetting.All : PrivateMessagePrivacySetting.OnlyFriends
            };

            var response = await socialServiceRPCProxy.StrictObject.Module()!
                                                      .CallUnaryProcedure<UpsertSocialSettingsResponse>("UpsertSocialSettings", payload)
                                                      .AttachExternalCancellation(ct)
                                                      .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

            if (response.ResponseCase == UpsertSocialSettingsResponse.ResponseOneofCase.Ok)
            {
                //if (!receiveAllMessages)
                //Send broadcast to all non-friends non-blocked that we wont accept their messages anymore
            }
            else
                throw new Exception($"Cannot upsert social settings: {response.ResponseCase}");
        }


        public async UniTask GetOwnSocialSettingsAsync(CancellationToken ct)
        {
            await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);

            var response = await socialServiceRPCProxy.StrictObject.Module()!
                                                      .CallUnaryProcedure<GetSocialSettingsResponse>("GetSocialSettings", new Empty())
                                                      .AttachExternalCancellation(ct)
                                                      .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

            //IF response OK Update setting in settings panel and in a cache I guess? so we know to block non-friends messages as well and send them a response

        }

        public async UniTask GetPrivacySettingForUsersAsync(IReadOnlyList<Web3Address> walletIds, CancellationToken ct)
        {
            await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);

            var users = new RepeatedField<User>();

            foreach (var wallet in walletIds)
                users.Add(new User { Address = wallet});

            var payload = new GetPrivateMessagesSettingsPayload
            {
                User = {users},
            };

            var response = await socialServiceRPCProxy.StrictObject.Module()!
                                                      .CallUnaryProcedure<GetPrivateMessagesSettingsResponse>("GetPrivateMessagesSettings", payload)
                                                      .AttachExternalCancellation(ct)
                                                      .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

            if (response.ResponseCase == GetPrivateMessagesSettingsResponse.ResponseOneofCase.Ok)
            {
                foreach (var setting in response.Ok.Settings)
                {
                    //Return IEnumerable with users that block non-friends connection
                }
            }
            else
                throw new Exception($"Cannot get privacy settings: {response.ResponseCase}");
        }

    }
}
