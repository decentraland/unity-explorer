using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
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
    public class RPCChatPrivacyService : IDisposable
    {
        private const double TIMEOUT_SECONDS = 30;
        private readonly IRPCSocialServices socialServiceRPCProxy;
        private readonly ChatSettingsAsset settingsAsset;
        private readonly ConnectionSubscription connectionSubscription;

        public RPCChatPrivacyService(
            IRPCSocialServices socialServiceRPCProxy,
            ChatSettingsAsset settingsAsset)
        {
            this.socialServiceRPCProxy = socialServiceRPCProxy;
            this.settingsAsset = settingsAsset;

            // Subscribe to centralized connection management
            connectionSubscription = socialServiceRPCProxy.SubscribeToConnection(CancellationToken.None);
            connectionSubscription.ConnectionFailed += OnConnectionFailed;
        }

        public void Dispose()
        {
            connectionSubscription?.Dispose();
        }

        private void OnConnectionFailed()
        {
            // Connection attempts exhausted - log error
            ReportHub.LogError(ReportCategory.CHAT_MESSAGES, "Chat privacy service connection failed - privacy settings may not be available");
        }

        public async UniTaskVoid UpsertSocialSettingsAsync(bool receiveAllMessages, CancellationToken ct)
        {
            try
            {
                // Wait for connection to be established
                await connectionSubscription.WaitForConnectionAsync(ct);

                var payload = new UpsertSocialSettingsPayload
                {
                    PrivateMessagesPrivacy = receiveAllMessages ? PrivateMessagePrivacySetting.All : PrivateMessagePrivacySetting.OnlyFriends,
                };

                UpsertSocialSettingsResponse? response = await socialServiceRPCProxy.Module()!
                                                                                    .CallUnaryProcedure<UpsertSocialSettingsResponse>("UpsertSocialSettings", payload)
                                                                                    .AttachExternalCancellation(ct)
                                                                                    .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

                if (response.ResponseCase != UpsertSocialSettingsResponse.ResponseOneofCase.Ok)
                    throw new Exception($"Cannot upsert social settings: {response.ResponseCase}");
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ReportHub.LogException(e, ReportCategory.CHAT_MESSAGES);
                throw new InvalidOperationException($"Failed to upsert social settings: {e.Message}", e);
            }
        }

        public async UniTask GetOwnSocialSettingsAsync(CancellationToken ct)
        {
            try
            {
                await connectionSubscription.WaitForConnectionAsync(ct);

                GetSocialSettingsResponse? response = await socialServiceRPCProxy.Module()!
                                                                                 .CallUnaryProcedure<GetSocialSettingsResponse>("GetSocialSettings", new Empty())
                                                                                 .AttachExternalCancellation(ct)
                                                                                 .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

                settingsAsset.OnPrivacyRead(response.Ok?.Settings.PrivateMessagesPrivacy == PrivateMessagePrivacySetting.OnlyFriends ? ChatPrivacySettings.ONLY_FRIENDS : ChatPrivacySettings.ALL);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ReportHub.LogException(e, ReportCategory.CHAT_MESSAGES);
                throw new InvalidOperationException($"Failed to get own social settings: {e.Message}", e);
            }
        }

        public async UniTask<PrivacySettingsForUsersPayload> GetPrivacySettingForUsersAsync(HashSet<string> walletIds, CancellationToken ct)
        {
            try
            {
                await connectionSubscription.WaitForConnectionAsync(ct);

                var users = new RepeatedField<User>();

                foreach (string wallet in walletIds)
                    users.Add(new User { Address = wallet });

                var payload = new GetPrivateMessagesSettingsPayload
                {
                    User = { users },
                };

                GetPrivateMessagesSettingsResponse? response = await socialServiceRPCProxy.Module()!
                                                                                          .CallUnaryProcedure<GetPrivateMessagesSettingsResponse>("GetPrivateMessagesSettings", payload)
                                                                                          .AttachExternalCancellation(ct)
                                                                                          .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

                var privacySettings = new PrivacySettingsForUsersPayload(true);

                if (response.ResponseCase == GetPrivateMessagesSettingsResponse.ResponseOneofCase.Ok)
                {
                    foreach (GetPrivateMessagesSettingsResponse.Types.PrivateMessagesSettings? setting in response.Ok.Settings)
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
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ReportHub.LogException(e, ReportCategory.CHAT_MESSAGES);
                throw new InvalidOperationException($"Failed to get privacy settings for users: {e.Message}", e);
            }
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
