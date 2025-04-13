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
            participants = new HashSet<string>[2];
            participants[0] = new HashSet<string>();
            participants[1] = new HashSet<string>();
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

        //TODO FRAN: Replace this HashSet<string>[] with a custom struct so its less prone to error
        public async UniTask<PrivacySettingsForUsersPayload> GetPrivacySettingForUsersAsync(HashSet<string> walletIds, CancellationToken ct)
        {
            await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);

            var users = new RepeatedField<User>();

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

            var privacySettings = new PrivacySettingsForUsersPayload();

            if (response.ResponseCase == GetPrivateMessagesSettingsResponse.ResponseOneofCase.Ok)
            {

                foreach (var setting in response.Ok.Settings)
                {
                    if (setting.PrivateMessagesPrivacy == PrivateMessagePrivacySetting.OnlyFriends)
                        privacySettings.OnlyFriends?.Add(setting.User.Address);
                    else
                        privacySettings.All?.Add(setting.User.Address);
                }
            }
            else
                throw new Exception($"Cannot get privacy settings: {response.ResponseCase}");

            return privacySettings;
        }

        public readonly struct PrivacySettingsForUsersPayload
        {
            public readonly HashSet<string> OnlyFriends;
            public readonly HashSet<string> All;

            public PrivacySettingsForUsersPayload(HashSet<string> onlyFriends, HashSet<string> all)
            {
                OnlyFriends = new HashSet<string>();
                All = new HashSet<string>();
            }
        }
    }
}
