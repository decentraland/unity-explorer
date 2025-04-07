using Cysharp.Threading.Tasks;
using DCL.Settings.Settings;
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
        private readonly ObjectProxy<IRPCSocialServices> socialServiceRPCProxy;
        private readonly ChatSettingsAsset settingsAsset;

        private readonly HashSet<string>[] participants;

        public RPCChatPrivacyService(
            ObjectProxy<IRPCSocialServices> socialServiceRPCProxy,
            ChatSettingsAsset settingsAsset)
        {
            this.socialServiceRPCProxy = socialServiceRPCProxy;
            this.settingsAsset = settingsAsset;
            this.participants = new HashSet<string>[2];
        }

        private const int TIMEOUT_SECONDS = 30;

        public async UniTaskVoid UpsertSocialSettingsAsync(bool receiveAllMessages, CancellationToken ct)
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

            if (response.ResponseCase != UpsertSocialSettingsResponse.ResponseOneofCase.Ok)
                throw new Exception($"Cannot upsert social settings: {response.ResponseCase}");
        }


        public async UniTask GetOwnSocialSettingsAsync(CancellationToken ct)
        {
            await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);

            var response = await socialServiceRPCProxy.StrictObject.Module()!
                                                      .CallUnaryProcedure<GetSocialSettingsResponse>("GetSocialSettings", new Empty())
                                                      .AttachExternalCancellation(ct)
                                                      .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

            settingsAsset.OnPrivacyRead(response.Ok?.Settings.PrivateMessagesPrivacy == PrivateMessagePrivacySetting.OnlyFriends ? ChatPrivacySettings.ONLY_FRIENDS : ChatPrivacySettings.ALL);
        }

        public async UniTask<HashSet<string>[]> GetPrivacySettingForUsersAsync(HashSet<string> walletIds, CancellationToken ct)
        {
            await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);

            var users = new RepeatedField<User>();
            participants[0].Clear();
            participants[1].Clear();

            foreach (string wallet in walletIds)
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
                    if (setting.PrivateMessagesPrivacy == PrivateMessagePrivacySetting.OnlyFriends)
                        participants[0].Add(setting.User.Address);
                    else
                        participants[1].Add(setting.User.Address);
                }
            }
            else
                throw new Exception($"Cannot get privacy settings: {response.ResponseCase}");

            return participants;
        }
    }
}
