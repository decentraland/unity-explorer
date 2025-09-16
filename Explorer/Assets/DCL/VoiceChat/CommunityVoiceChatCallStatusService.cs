using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Notifications = DCL.NotificationsBusController.NotificationsBus;
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
    public class CommunityVoiceChatCallStatusService : ICommunityVoiceChatCallStatusService
    {
        private static readonly ReactiveProperty<bool> DEFAULT_BOOL_REACTIVE_PROPERTY = new (false);
        private const string TAG = nameof(CommunityVoiceChatCallStatusService);

        public IReadonlyReactiveProperty<VoiceChatStatus> Status => status;
        public IReadonlyReactiveProperty<string> CallId => callId1;
        string IVoiceChatCallStatusServiceBase.ConnectionUrl => connectionUrl;

        private readonly ReactiveProperty<VoiceChatStatus> status = new (VoiceChatStatus.DISCONNECTED);
        private readonly ReactiveProperty<string> callId1 = new (string.Empty);
        private string connectionUrl = string.Empty;

        private readonly ICommunityVoiceService voiceChatService;
        private readonly SceneVoiceChatTrackerService voiceChatSceneTrackerService;
        private readonly Dictionary<string, ReactiveProperty<bool>> communityVoiceChatCalls = new ();
        private readonly Dictionary<string, ActiveCommunityVoiceChat> activeCommunityVoiceChats = new ();

        private CancellationTokenSource cts = new ();
        private string? locallyStartedCommunityId ;

        public CommunityVoiceChatCallStatusService(
            ICommunityVoiceService voiceChatService,
            SceneVoiceChatTrackerService voiceChatSceneTrackerService)
        {
            this.voiceChatService = voiceChatService;
            this.voiceChatSceneTrackerService = voiceChatSceneTrackerService;

            this.voiceChatService.CommunityVoiceChatUpdateReceived += OnCommunityVoiceChatUpdateReceived;
            this.voiceChatService.ActiveCommunityVoiceChatsFetched += OnActiveCommunityVoiceChatsFetched;
        }

        public void StartCall(string communityId)
        {
            //We can start a call only if we are not connected or trying to start a call
            if (!status.Value.IsNotConnected()) return;

            cts = cts.SafeRestart();

            //Setting starting call status to instantly disable call button
            UpdateStatus(VoiceChatStatus.VOICE_CHAT_STARTING_CALL);

            // Track that we started this community call
            locallyStartedCommunityId = communityId;

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
                        connectionUrl = response.Ok.Credentials.ConnectionUrl;
                        SetCallId(communityId);
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

        public void HangUp()
        {
            ResetVoiceChatData();
            UpdateStatus(VoiceChatStatus.VOICE_CHAT_ENDING_CALL);
            locallyStartedCommunityId = null;
        }

        public async UniTaskVoid JoinCommunityVoiceChatAsync(string communityId, CancellationToken ct)
        {
            try
            {
                if (!status.Value.IsNotConnected())
                    //we should throw here and let the catch handle it?
                    return;

                UpdateStatus(VoiceChatStatus.VOICE_CHAT_STARTING_CALL);

                JoinCommunityVoiceChatResponse response = await voiceChatService.JoinCommunityVoiceChatAsync(communityId, ct);

                switch (response.ResponseCase)
                {
                    case JoinCommunityVoiceChatResponse.ResponseOneofCase.Ok:
                        connectionUrl = response.Ok.Credentials.ConnectionUrl;
                        SetCallId(communityId);
                        UpdateStatus(VoiceChatStatus.VOICE_CHAT_IN_CALL);
                        break;
                    default:
                        ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Error when connecting to call {response}");
                        ResetVoiceChatData();
                        UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
                        break;
                }
            }
            catch (Exception e) { }
        }

        public void RequestToSpeakInCurrentCall()
        {
            if (status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL || string.IsNullOrEmpty(callId1.Value)) return;

            cts = cts.SafeRestart();
            RequestToSpeakAsync(callId1.Value, cts.Token).Forget();
            return;

            async UniTaskVoid RequestToSpeakAsync(string communityId, CancellationToken ct)
            {
                try
                {
                    RequestToSpeakInCommunityVoiceChatResponse response = await voiceChatService.RequestToSpeakInCommunityVoiceChatAsync(communityId, true, ct);

                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} RequestToSpeak response: {response.ResponseCase} for community {communityId}");
                }
                catch (Exception e) { }
            }
        }

        public void LowerHandInCurrentCall()
        {
            if (status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL || string.IsNullOrEmpty(callId1.Value)) return;

            cts = cts.SafeRestart();
            LowerHandAsync(callId1.Value, cts.Token).Forget();
            return;

            async UniTaskVoid LowerHandAsync(string communityId, CancellationToken ct)
            {
                try
                {
                    RequestToSpeakInCommunityVoiceChatResponse response = await voiceChatService.RequestToSpeakInCommunityVoiceChatAsync(communityId, false, ct);

                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} RequestToSpeak response: {response.ResponseCase} for community {communityId}");
                }
                catch (Exception e) { }
            }
        }

        public void PromoteToSpeakerInCurrentCall(string walletId)
        {
            if (string.IsNullOrEmpty(callId1.Value)) return;
            if (status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL) return;

            cts = cts.SafeRestart();
            PromoteToSpeakerAsync(callId1.Value).Forget();
            return;

            async UniTaskVoid PromoteToSpeakerAsync(string communityId)
            {
                try
                {
                    PromoteSpeakerInCommunityVoiceChatResponse response = await voiceChatService.PromoteSpeakerInCommunityVoiceChatAsync(communityId, walletId, cts.Token);

                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} PromoteToSpeaker response: {response.ResponseCase} for community {communityId}, wallet {walletId}");
                }
                catch (Exception e) { }
            }
        }

        public void DenySpeakerInCurrentCall(string walletId)
        {
            if (string.IsNullOrEmpty(callId1.Value)) return;
            if (status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL) return;

            cts = cts.SafeRestart();
            DenySpeakerAsync(callId1.Value, walletId, cts.Token).Forget();

            return;

            async UniTaskVoid DenySpeakerAsync(string communityId, string walletId, CancellationToken ct)
            {
                try
                {
                    RejectSpeakRequestInCommunityVoiceChatResponse response = await voiceChatService.DenySpeakerInCommunityVoiceChatAsync(communityId, walletId, ct);

                    switch (response.ResponseCase)
                    {
                        case RejectSpeakRequestInCommunityVoiceChatResponse.ResponseOneofCase.Ok:
                            break;
                    }
                }
                catch (Exception e) { }
            }
        }

        public void DemoteFromSpeakerInCurrentCall(string walletId)
        {
            if (string.IsNullOrEmpty(callId1.Value)) return;
            if (status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL) return;

            cts = cts.SafeRestart();
            DemoteFromSpeakerAsync(callId1.Value, cts.Token).Forget();
            return;

            async UniTaskVoid DemoteFromSpeakerAsync(string communityId, CancellationToken ct)
            {
                try
                {
                    DemoteSpeakerInCommunityVoiceChatResponse response = await voiceChatService.DemoteSpeakerInCommunityVoiceChatAsync(communityId, walletId, ct);

                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} DemoteFromSpeaker response: {response.ResponseCase} for community {communityId}, wallet {walletId}");
                }
                catch (Exception e) { }
            }
        }

        public void KickPlayerFromCurrentCall(string walletId)
        {
            if (string.IsNullOrEmpty(callId1.Value)) return;
            if (status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL) return;

            cts = cts.SafeRestart();
            KickPlayerAsync(callId1.Value, walletId, cts.Token).Forget();
            return;

            async UniTaskVoid KickPlayerAsync(string communityId, string walletId, CancellationToken ct)
            {
                try
                {
                    KickPlayerFromCommunityVoiceChatResponse response = await voiceChatService.KickPlayerFromCommunityVoiceChatAsync(communityId, walletId, ct);

                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} KickPlayer response: {response.ResponseCase} for community {communityId}, wallet {walletId}");
                }
                catch (Exception e) { }
            }
        }

        public void EndStreamInCurrentCall()
        {
            if (string.IsNullOrEmpty(callId1.Value)) return;
            if (status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL) return;

            cts = cts.SafeRestart();
            EndStreamAsync(callId1.Value, cts.Token).Forget();
            return;

            async UniTaskVoid EndStreamAsync(string communityId, CancellationToken ct)
            {
                try
                {
                    EndCommunityVoiceChatResponse response = await voiceChatService.EndCommunityVoiceChatAsync(communityId, ct);

                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} End stream response: {response.ResponseCase} for community {communityId}");
                }
                catch (Exception e) { }
            }
        }

        public void HandleLivekitConnectionFailed()
        {
            ResetVoiceChatData();
            UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
        }

        public void HandleLivekitConnectionEnded()
        {
            if (status.Value == VoiceChatStatus.DISCONNECTED) return;

            ResetVoiceChatData();
            UpdateStatus(VoiceChatStatus.DISCONNECTED);
        }

        public void UpdateStatus(VoiceChatStatus newStatus)
        {
            UpdateStatusAsync().Forget();

            async UniTaskVoid UpdateStatusAsync()
            {
                await UniTask.SwitchToMainThread();
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} New status is {newStatus}");
                status.Value = newStatus;
            }
        }

        public void ResetVoiceChatData()
        {
            communityVoiceChatCalls.Clear();
            activeCommunityVoiceChats.Clear();
            locallyStartedCommunityId = null;
        }

        public void SetCallId(string newCallId)
        {
            callId1.Value = newCallId;
        }

        private void OnCommunityVoiceChatUpdateReceived(CommunityVoiceChatUpdate communityUpdate)
        {
            if (string.IsNullOrEmpty(communityUpdate.CommunityId))
            {
                ReportHub.LogWarning(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Received community voice chat update with empty community ID");
                return;
            }

            ReportHub.LogWarning(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Received community update for {communityUpdate.CommunityId} with status: {communityUpdate.Status.ToString()} with positions: {communityUpdate.Positions}");

            if (communityUpdate.Status == CommunityVoiceChatStatus.CommunityVoiceChatEnded)
            {
                activeCommunityVoiceChats.Remove(communityUpdate.CommunityId);

                if (communityVoiceChatCalls.TryGetValue(communityUpdate.CommunityId, out ReactiveProperty<bool>? existingData))
                {
                    existingData.Value = false;
                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Community voice chat ended for {communityUpdate.CommunityId}");
                }

                // Clear locally started community ID if this was the one we started
                if (communityUpdate.CommunityId == locallyStartedCommunityId)
                    locallyStartedCommunityId = null;

                // Delegate scene unregistration to the tracker
                voiceChatSceneTrackerService.UnregisterCommunityFromScene(communityUpdate.CommunityId);
                voiceChatSceneTrackerService.RemoveActiveCommunityVoiceChat(communityUpdate.CommunityId);
                return;
            }

            var activeChat = new ActiveCommunityVoiceChat
            {
                communityId = communityUpdate.CommunityId,
                communityName = communityUpdate.CommunityName,
                communityImage = communityUpdate.CommunityImage,
                isMember = communityUpdate.IsMember,
                positions = new List<string>(communityUpdate.Positions),
                worlds = new List<string>(communityUpdate.Worlds),
                participantCount = 0, // This would need to be populated from other sources
                moderatorCount = 0, // This would need to be populated from other sources
            };

            // Update the active community voice chats dictionary
            activeCommunityVoiceChats[communityUpdate.CommunityId] = activeChat;

            if (communityVoiceChatCalls.TryGetValue(communityUpdate.CommunityId, out ReactiveProperty<bool>? existingCallData))
            {
                existingCallData.Value = communityUpdate.Status == CommunityVoiceChatStatus.CommunityVoiceChatStarted;
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Updated community {communityUpdate.CommunityId}");
            }
            else
            {
                communityVoiceChatCalls[communityUpdate.CommunityId] = new ReactiveProperty<bool>(communityUpdate.Status == CommunityVoiceChatStatus.CommunityVoiceChatStarted);
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Added community {communityUpdate.CommunityId}");
            }

            if (communityUpdate.Status == CommunityVoiceChatStatus.CommunityVoiceChatStarted)
            {
                // Delegate scene registration to the tracker
                voiceChatSceneTrackerService.RegisterCommunityInScene(communityUpdate.CommunityId, communityUpdate.Positions, communityUpdate.Worlds);
                voiceChatSceneTrackerService.SetActiveCommunityVoiceChat(communityUpdate.CommunityId, activeChat);

                // We only show notification if we are part of the community, and we didn't start the stream ourselves
                if (communityUpdate.IsMember && communityUpdate.CommunityId != locallyStartedCommunityId)
                    Notifications.NotificationsBusController.Instance.AddNotification(new CommunityVoiceChatStartedNotification(communityUpdate.CommunityName, communityUpdate.CommunityImage, communityUpdate.CommunityId));
            }
        }

        private void OnActiveCommunityVoiceChatsFetched(ActiveCommunityVoiceChatsResponse response)
        {
            if (response.data.activeChats == null)
            {
                ReportHub.LogWarning(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Received null or empty active community voice chats data");
                return;
            }

            foreach (ActiveCommunityVoiceChat activeChat in response.data.activeChats)
            {
                if (string.IsNullOrEmpty(activeChat.communityId))
                {
                    ReportHub.LogWarning(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Skipping active community voice chat with empty community ID");
                    continue;
                }

                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Processing community {activeChat.communityId} - Name: {activeChat.communityName}, Participants: {activeChat.participantCount}, IsMember: {activeChat.isMember}, Positions: {activeChat.positions}");

                activeCommunityVoiceChats[activeChat.communityId] = activeChat;

                // Delegate scene registration to the tracker
                voiceChatSceneTrackerService.RegisterCommunityInScene(activeChat.communityId, activeChat.positions, activeChat.worlds);
                voiceChatSceneTrackerService.SetActiveCommunityVoiceChat(activeChat.communityId, activeChat);

                // Ensure we have a reactive property for this community
                if (!communityVoiceChatCalls.TryGetValue(activeChat.communityId, out ReactiveProperty<bool>? communityVoiceChatCall))
                {
                    communityVoiceChatCalls[activeChat.communityId] = new ReactiveProperty<bool>(true);
                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Added new community {activeChat.communityId} from active chats fetch");
                }
                else
                {
                    // Update existing reactive property to reflect active status
                    communityVoiceChatCall.Value = true;
                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Updated existing community {activeChat.communityId} from active chats fetch");
                }
            }

            ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Completed processing. Total communities in cache: {communityVoiceChatCalls.Count}");
        }

        public void Dispose()
        {
            foreach (ReactiveProperty<bool>? callData in communityVoiceChatCalls.Values) { callData.ClearSubscriptionsList(); }

            communityVoiceChatCalls.Clear();
            activeCommunityVoiceChats.Clear();
            locallyStartedCommunityId = null;
            cts.SafeCancelAndDispose();

            voiceChatService.CommunityVoiceChatUpdateReceived -= OnCommunityVoiceChatUpdateReceived;
            voiceChatService.ActiveCommunityVoiceChatsFetched -= OnActiveCommunityVoiceChatsFetched;
        }

        public bool HasActiveVoiceChatCall(string communityId)
        {
            if (string.IsNullOrEmpty(communityId)) { return false; }

            return communityVoiceChatCalls.TryGetValue(communityId, out ReactiveProperty<bool>? callData) && callData.Value;
        }

        public ReactiveProperty<bool>? SubscribeToCommunityUpdates(string communityId)
        {
            if (string.IsNullOrEmpty(communityId))
                return null;

            if (communityVoiceChatCalls.TryGetValue(communityId, out ReactiveProperty<bool>? existingData))
            {
                return existingData;
            }

            // Create new call data with no active call
            var newCallData = new ReactiveProperty<bool>(false);
            communityVoiceChatCalls[communityId] = newCallData;

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
