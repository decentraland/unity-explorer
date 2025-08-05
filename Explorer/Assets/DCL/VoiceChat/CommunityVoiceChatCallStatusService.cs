#nullable enable
using Castle.Core.Internal;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.Utilities;
using DCL.VoiceChat.Services;
using Decentraland.SocialService.V2;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Implementation of voice chat call status service for community calls.
    /// </summary>
    public class CommunityVoiceChatCallStatusService : VoiceChatCallStatusServiceBase, ICommunityVoiceChatCallStatusService
    {
        private readonly ICommunityVoiceService voiceChatService;
        private readonly INotificationsBusController notificationBusController;
        private readonly Dictionary<string, ReactiveProperty<bool>> communityVoiceChatCalls = new ();
        private readonly Dictionary<string, ActiveCommunityVoiceChat> activeCommunityVoiceChats = new ();

        private CancellationTokenSource cts = new ();

        public CommunityVoiceChatCallStatusService(
            ICommunityVoiceService voiceChatService,
            INotificationsBusController notificationBusController)
        {
            this.voiceChatService = voiceChatService;
            this.notificationBusController = notificationBusController;
            this.voiceChatService.CommunityVoiceChatUpdateReceived += OnCommunityVoiceChatUpdateReceived;
            this.voiceChatService.ActiveCommunityVoiceChatsFetched += OnActiveCommunityVoiceChatsFetched;
        }

        public override void StartCall(string communityId)
        {
            //We can start a call only if we are not connected or trying to start a call
            if (Status.Value is not VoiceChatStatus.DISCONNECTED and not VoiceChatStatus.VOICE_CHAT_BUSY and not VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR) return;

            cts = cts.SafeRestart();

            //Setting starting call status to instantly disable call button
            UpdateStatus(VoiceChatStatus.VOICE_CHAT_STARTING_CALL);

            StartCallAsync(communityId, cts.Token).Forget();
        }

        private async UniTaskVoid StartCallAsync(string communityId, CancellationToken ct)
        {
            try
            {
                StartCommunityVoiceChatResponse response = await voiceChatService.StartCommunityVoiceChatAsync(communityId, ct);

                //We switch to main thread to avoid reactive properties updates causing issues with UI.
                await UniTask.SwitchToMainThread();

                switch (response.ResponseCase)
                {
                    //When the call can be started
                    case StartCommunityVoiceChatResponse.ResponseOneofCase.Ok:
                        ConnectionUrl = response.Ok.Credentials.ConnectionUrl;
                        CallId = communityId;
                        UpdateStatus(VoiceChatStatus.VOICE_CHAT_IN_CALL);
                        break;

                    case StartCommunityVoiceChatResponse.ResponseOneofCase.InvalidRequest:
                    case StartCommunityVoiceChatResponse.ResponseOneofCase.ConflictingError:
                        ResetVoiceChatData();
                        UpdateStatus(VoiceChatStatus.VOICE_CHAT_BUSY);
                        break;
                    default:
                        ResetVoiceChatData();
                        UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
                        break;
                }
            }
            catch (Exception e) { }
        }

        public override void HangUp()
        {
            //TODO: currently just exits, need to figure out how to handle hang up
            ResetVoiceChatData();
            UpdateStatus(VoiceChatStatus.DISCONNECTED);
        }

        public async UniTaskVoid JoinCommunityVoiceChatAsync(string communityId, CancellationToken ct)
        {
            try
            {
                if (Status.Value is not VoiceChatStatus.DISCONNECTED and not VoiceChatStatus.VOICE_CHAT_BUSY and not VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR)
                    //we should throw here and let the catch handle it?
                    return;

                UpdateStatus(VoiceChatStatus.VOICE_CHAT_STARTING_CALL);

                JoinCommunityVoiceChatResponse response = await voiceChatService.JoinCommunityVoiceChatAsync(communityId, ct);

                switch (response.ResponseCase)
                {
                    case JoinCommunityVoiceChatResponse.ResponseOneofCase.Ok:
                        ConnectionUrl = response.Ok.Credentials.ConnectionUrl;
                        CallId = communityId;
                        UpdateStatus(VoiceChatStatus.VOICE_CHAT_IN_CALL);
                        break;
                    default:
                        ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"Error when connecting to call {response}");
                        ResetVoiceChatData();
                        UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
                        break;
                }
            }
            catch (Exception e) { }
        }

        public void RequestToSpeakInCurrentCall()
        {
            if (Status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL || CallId == null) return;

            cts = cts.SafeRestart();
            RequestToSpeakAsync(CallId, cts.Token).Forget();
        }

        private async UniTaskVoid RequestToSpeakAsync(string communityId, CancellationToken ct)
        {
            try
            {
                RequestToSpeakInCommunityVoiceChatResponse response = await voiceChatService.RequestToSpeakInCommunityVoiceChatAsync(communityId, ct);

                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"RequestToSpeak response: {response.ResponseCase} for community {communityId}");
            }
            catch (Exception e) { }
        }

        public void PromoteToSpeakerInCurrentCall(string walletId)
        {
            if (CallId.IsNullOrEmpty()) return;
            PromoteToSpeaker(CallId, walletId);
        }


        public void DenySpeakerInCurrentCall(string walletId)
        {
            if (CallId.IsNullOrEmpty()) return;
            DenySpeaker(CallId, walletId);
        }

        private void PromoteToSpeaker(string communityId, string walletId)
        {
            if (Status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL) return;

            cts = cts.SafeRestart();
            PromoteToSpeakerAsync(communityId, walletId, cts.Token).Forget();
        }

        private void DenySpeaker(string communityId, string walletId)
        {
            if (Status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL) return;

            cts = cts.SafeRestart();
            DenySpeakerAsync(communityId, walletId, cts.Token).Forget();
        }

        private async UniTaskVoid DenySpeakerAsync(string communityId, string walletId, CancellationToken ct)
        {
            try
            {
                PromoteSpeakerInCommunityVoiceChatResponse response = await voiceChatService.PromoteSpeakerInCommunityVoiceChatAsync(communityId, walletId, ct);

                switch (response.ResponseCase)
                {
                    case PromoteSpeakerInCommunityVoiceChatResponse.ResponseOneofCase.Ok:
                        //Handle promote logic here
                        break;
                }
            }
            catch (Exception e) { }
        }

        private async UniTaskVoid PromoteToSpeakerAsync(string communityId, string walletId, CancellationToken ct)
        {
            try
            {
                PromoteSpeakerInCommunityVoiceChatResponse response = await voiceChatService.PromoteSpeakerInCommunityVoiceChatAsync(communityId, walletId, ct);

                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"PromoteToSpeaker response: {response.ResponseCase} for community {communityId}, wallet {walletId}");
            }
            catch (Exception e) { }
        }

        public void DemoteFromSpeakerInCurrentCall(string walletId)
        {
            if (CallId.IsNullOrEmpty()) return;

            DemoteFromSpeaker(CallId, walletId);
        }

        private void DemoteFromSpeaker(string communityId, string walletId)
        {
            if (Status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL) return;

            cts = cts.SafeRestart();
            DemoteFromSpeakerAsync(communityId, walletId, cts.Token).Forget();
        }

        private async UniTaskVoid DemoteFromSpeakerAsync(string communityId, string walletId, CancellationToken ct)
        {
            try
            {
                DemoteSpeakerInCommunityVoiceChatResponse response = await voiceChatService.DemoteSpeakerInCommunityVoiceChatAsync(communityId, walletId, ct);

                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"DemoteFromSpeaker response: {response.ResponseCase} for community {communityId}, wallet {walletId}");
            }
            catch (Exception e) { }
        }

        public void KickPlayerFromCurrentCall(string walletId)
        {
            if (CallId.IsNullOrEmpty()) return;

            KickPlayer(CallId, walletId);
        }

        private void KickPlayer(string communityId, string walletId)
        {
            if (Status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL) return;

            cts = cts.SafeRestart();
            KickPlayerAsync(communityId, walletId, cts.Token).Forget();
        }

        private async UniTaskVoid KickPlayerAsync(string communityId, string walletId, CancellationToken ct)
        {
            try
            {
                KickPlayerFromCommunityVoiceChatResponse response = await voiceChatService.KickPlayerFromCommunityVoiceChatAsync(communityId, walletId, ct);

                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"KickPlayer response: {response.ResponseCase} for community {communityId}, wallet {walletId}");
            }
            catch (Exception e) { }
        }

        public override void HandleLivekitConnectionFailed()
        {
            ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, "Community voice chat HandleLivekitConnectionFailed not yet implemented");
            UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
        }

        private void OnCommunityVoiceChatUpdateReceived(CommunityVoiceChatUpdate communityUpdate)
        {
            if (string.IsNullOrEmpty(communityUpdate.CommunityId))
            {
                ReportHub.LogWarning(ReportCategory.COMMUNITY_VOICE_CHAT, "Received community voice chat update with empty community ID");
                return;
            }

            if (communityUpdate.Status == CommunityVoiceChatStatus.CommunityVoiceChatEnded)
            {
                // Remove from active community voice chats when the call ends
                activeCommunityVoiceChats.Remove(communityUpdate.CommunityId);

                if (communityVoiceChatCalls.TryGetValue(communityUpdate.CommunityId, out ReactiveProperty<bool>? existingData))
                {
                    existingData.Value = false;
                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"Community voice chat ended for {communityUpdate.CommunityId}");
                }
                return;
            }

            // Update or add the active community voice chat information from the update
            var activeChat = new ActiveCommunityVoiceChat
            {
                communityId = communityUpdate.CommunityId,
                communityName = communityUpdate.CommunityName,
                communityImage = communityUpdate.CommunityImage,
                isMember = communityUpdate.IsMember,
                positions = new List<string>(communityUpdate.Positions),
                worlds = string.Join(",", communityUpdate.Worlds),
                participantCount = 0, // This would need to be populated from other sources
                moderatorCount = 0 // This would need to be populated from other sources
            };

            // Update the active community voice chats dictionary
            activeCommunityVoiceChats[communityUpdate.CommunityId] = activeChat;

            if (communityVoiceChatCalls.TryGetValue(communityUpdate.CommunityId, out ReactiveProperty<bool>? existingCallData))
            {
                existingCallData.Value = communityUpdate.Status == CommunityVoiceChatStatus.CommunityVoiceChatStarted;
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"Updated community {communityUpdate.CommunityId}");
            }
            else
            {
                communityVoiceChatCalls[communityUpdate.CommunityId] = new ReactiveProperty<bool>(communityUpdate.Status == CommunityVoiceChatStatus.CommunityVoiceChatStarted);
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"Added community {communityUpdate.CommunityId}");
            }

            notificationBusController.AddNotification(new CommunityVoiceChatStartedNotification(communityUpdate.CommunityName, communityUpdate.CommunityImage));
        }

        private void OnActiveCommunityVoiceChatsFetched(ActiveCommunityVoiceChatsResponse response)
        {
            if (response.data.activeChats == null)
            {
                ReportHub.LogWarning(ReportCategory.COMMUNITY_VOICE_CHAT, "Received null or empty active community voice chats data");
                return;
            }

            ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"Processing {response.data.activeChats.Count} active community voice chats");

            foreach (var activeChat in response.data.activeChats)
            {
                if (string.IsNullOrEmpty(activeChat.communityId))
                {
                    ReportHub.LogWarning(ReportCategory.COMMUNITY_VOICE_CHAT, "Skipping active community voice chat with empty community ID");
                    continue;
                }

                activeCommunityVoiceChats[activeChat.communityId] = activeChat;

                // Ensure we have a reactive property for this community
                if (!communityVoiceChatCalls.TryGetValue(activeChat.communityId, out ReactiveProperty<bool>? communityVoiceChatCall))
                {
                    communityVoiceChatCalls[activeChat.communityId] = new ReactiveProperty<bool>(true);
                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"Added new community {activeChat.communityId} from active chats fetch");
                }
                else
                {
                    // Update existing reactive property to reflect active status
                    communityVoiceChatCall.Value = true;
                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"Updated existing community {activeChat.communityId} from active chats fetch");
                }
            }
        }

        public override void Dispose()
        {
            voiceChatService.CommunityVoiceChatUpdateReceived -= OnCommunityVoiceChatUpdateReceived;
            voiceChatService.ActiveCommunityVoiceChatsFetched -= OnActiveCommunityVoiceChatsFetched;
            voiceChatService.Dispose();

            foreach (ReactiveProperty<bool>? callData in communityVoiceChatCalls.Values) { callData.Dispose(); }

            communityVoiceChatCalls.Clear();
            activeCommunityVoiceChats.Clear();

            cts.SafeCancelAndDispose();
            base.Dispose();
        }

        public bool HasActiveVoiceChatCall(string communityId)
        {
            if (string.IsNullOrEmpty(communityId)) { return false; }

            return communityVoiceChatCalls.TryGetValue(communityId, out ReactiveProperty<bool>? callData) && callData.Value;
        }

        public ReactiveProperty<bool> SubscribeToCommunityUpdates(string communityId)
        {
            if (string.IsNullOrEmpty(communityId))
                return null;

            if (communityVoiceChatCalls.TryGetValue(communityId, out ReactiveProperty<bool>? existingData))
            {
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"Returning existing subscription for community {communityId}");
                return existingData;
            }

            // Create new call data with no active call
            var newCallData = new ReactiveProperty<bool>(false);
            communityVoiceChatCalls[communityId] = newCallData;

            ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"Created new subscription for community {communityId}");
            return newCallData;
        }

        public bool TryGetActiveCommunityVoiceChat(string communityId, out ActiveCommunityVoiceChat activeCommunityVoiceChat)
        {
            if (string.IsNullOrEmpty(communityId))
            {
                activeCommunityVoiceChat = default(ActiveCommunityVoiceChat);
                return false;
            }

            return activeCommunityVoiceChats.TryGetValue(communityId, out activeCommunityVoiceChat);
        }
    }
}
