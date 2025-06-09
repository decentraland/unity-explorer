using Cysharp.Threading.Tasks;
using DCL.Settings.Settings;
using DCL.SocialService;
using DCL.Utilities;
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
        private readonly IRPCSocialServices socialServiceRPCProxy;
        private readonly ChatSettingsAsset settingsAsset;

        public RPCChatPrivacyService(
            IRPCSocialServices socialServiceRPCProxy,
            ChatSettingsAsset settingsAsset)
        {
            this.socialServiceRPCProxy = socialServiceRPCProxy;
            this.settingsAsset = settingsAsset;
        }

        private const double TIMEOUT_SECONDS = 30;

        public async UniTaskVoid UpsertSocialSettingsAsync(bool receiveAllMessages, CancellationToken ct)
        {
            await socialServiceRPCProxy.EnsureRpcConnectionAsync(ct);

            var payload = new UpsertSocialSettingsPayload
            {
                PrivateMessagesPrivacy = receiveAllMessages? PrivateMessagePrivacySetting.All : PrivateMessagePrivacySetting.OnlyFriends
            };

            var response = await socialServiceRPCProxy.Module()!
                                                      .CallUnaryProcedure<UpsertSocialSettingsResponse>("UpsertSocialSettings", payload)
                                                      .AttachExternalCancellation(ct)
                                                      .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

            if (response.ResponseCase != UpsertSocialSettingsResponse.ResponseOneofCase.Ok)
                throw new Exception($"Cannot upsert social settings: {response.ResponseCase}");
        }


        public async UniTask GetOwnSocialSettingsAsync(CancellationToken ct)
        {
            await socialServiceRPCProxy.EnsureRpcConnectionAsync(ct);

            var response = await socialServiceRPCProxy.Module()!
                                                      .CallUnaryProcedure<GetSocialSettingsResponse>("GetSocialSettings", new Empty())
                                                      .AttachExternalCancellation(ct)
                                                      .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

            settingsAsset.OnPrivacyRead(response.Ok?.Settings.PrivateMessagesPrivacy == PrivateMessagePrivacySetting.OnlyFriends ? ChatPrivacySettings.ONLY_FRIENDS : ChatPrivacySettings.ALL);
        }

        public async UniTask<PrivacySettingsForUsersPayload> GetPrivacySettingForUsersAsync(HashSet<string> walletIds, CancellationToken ct)
        {
            await socialServiceRPCProxy.EnsureRpcConnectionAsync(ct);

            var users = new RepeatedField<User>();

            foreach (string wallet in walletIds)
                users.Add(new User { Address = wallet});

            var payload = new GetPrivateMessagesSettingsPayload
            {
                User = {users},
            };

            var response = await socialServiceRPCProxy.Module()!
                                                      .CallUnaryProcedure<GetPrivateMessagesSettingsResponse>("GetPrivateMessagesSettings", payload)
                                                      .AttachExternalCancellation(ct)
                                                      .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

            var privacySettings = new PrivacySettingsForUsersPayload(true);

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

            public PrivacySettingsForUsersPayload(bool _)
            {
                OnlyFriends = new HashSet<string>();
                All = new HashSet<string>();
            }
        }
    }
}
