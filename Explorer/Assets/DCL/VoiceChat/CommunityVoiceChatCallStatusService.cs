using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using DCL.VoiceChat.Services;
using Decentraland.SocialService.V2;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.VoiceChat
{
    public class CommunityVoiceChatCallStatusService : ICommunityVoiceChatCallStatusService
    {
        private const string TAG = nameof(CommunityVoiceChatCallStatusService);

        private readonly ReactiveProperty<VoiceChatStatus> status = new (VoiceChatStatus.DISCONNECTED);
        private readonly ReactiveProperty<string> callId = new (string.Empty);

        private readonly ICommunityVoiceService voiceChatService;
        private readonly SceneVoiceChatTrackerService voiceChatSceneTrackerService;
        private readonly Dictionary<string, ReactiveProperty<bool>> communityVoiceChatCalls = new ();
        private readonly Dictionary<string, ActiveCommunityVoiceChat> activeCommunityVoiceChats = new ();
        private string connectionUrl = string.Empty;

        private CancellationTokenSource cts = new ();
        private string? locallyStartedCommunityId;

        public IReadonlyReactiveProperty<VoiceChatStatus> Status => status;
        public IReadonlyReactiveProperty<string> CallId => callId;
        string IVoiceChatCallStatusServiceBase.ConnectionUrl => connectionUrl;

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

            UpdateStatus(VoiceChatStatus.VOICE_CHAT_STARTING_CALL);

            // Track that WE started this community call
            locallyStartedCommunityId = communityId;

            StartCallAsync(communityId, cts.Token).Forget();
        }

        private async UniTaskVoid StartCallAsync(string communityId, CancellationToken ct)
        {
            Result<StartCommunityVoiceChatResponse> result = await voiceChatService.StartCommunityVoiceChatAsync(communityId, ct)
                                                                                .SuppressToResultAsync(ReportCategory.COMMUNITY_VOICE_CHAT);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success)
                return;

            //We switch to main thread to avoid reactive properties updates causing issues with UI.
            await UniTask.SwitchToMainThread();

            switch (result.Value.ResponseCase)
            {
                //When the call can be started
                case StartCommunityVoiceChatResponse.ResponseOneofCase.Ok:
                    connectionUrl = result.Value.Ok.Credentials.ConnectionUrl;
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

        public void HangUp()
        {
            ResetVoiceChatData();
            UpdateStatus(VoiceChatStatus.VOICE_CHAT_ENDING_CALL);
        }

        public async UniTaskVoid JoinCommunityVoiceChatAsync(string communityId, CancellationToken ct)
        {
            if (!status.Value.IsNotConnected())
                return;

            UpdateStatus(VoiceChatStatus.VOICE_CHAT_STARTING_CALL);

            Result<JoinCommunityVoiceChatResponse> result = await voiceChatService.JoinCommunityVoiceChatAsync(communityId, ct)
                                                                                 .SuppressToResultAsync(ReportCategory.COMMUNITY_VOICE_CHAT);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success)
            {
                ResetVoiceChatData();
                UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
                return;
            }

            switch (result.Value.ResponseCase)
            {
                case JoinCommunityVoiceChatResponse.ResponseOneofCase.Ok:
                    connectionUrl = result.Value.Ok.Credentials.ConnectionUrl;
                    SetCallId(communityId);
                    UpdateStatus(VoiceChatStatus.VOICE_CHAT_IN_CALL);
                    break;
                default:
                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Error when connecting to call {result.Value}");
                    ResetVoiceChatData();
                    UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
                    break;
            }
        }

        public void RequestToSpeakInCurrentCall()
        {
            if (status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL || string.IsNullOrEmpty(callId.Value)) return;

            cts = cts.SafeRestart();
            RequestToSpeakAsync(callId.Value, cts.Token).Forget();
            return;

            async UniTaskVoid RequestToSpeakAsync(string communityId, CancellationToken ct)
            {
                Result<RequestToSpeakInCommunityVoiceChatResponse> result = await voiceChatService.RequestToSpeakInCommunityVoiceChatAsync(communityId, true, ct)
                                                                                                    .SuppressToResultAsync(ReportCategory.COMMUNITY_VOICE_CHAT);

                if (ct.IsCancellationRequested)
                    return;

                if (result.Success)
                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} RequestToSpeak response: {result.Value.ResponseCase} for community {communityId}");
            }
        }

        public void LowerHandInCurrentCall()
        {
            if (status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL || string.IsNullOrEmpty(callId.Value)) return;

            cts = cts.SafeRestart();
            LowerHandAsync(callId.Value, cts.Token).Forget();
            return;

            async UniTaskVoid LowerHandAsync(string communityId, CancellationToken ct)
            {
                Result<RequestToSpeakInCommunityVoiceChatResponse> result = await voiceChatService.RequestToSpeakInCommunityVoiceChatAsync(communityId, false, ct)
                                                                                                    .SuppressToResultAsync(ReportCategory.COMMUNITY_VOICE_CHAT);

                if (ct.IsCancellationRequested)
                    return;

                if (result.Success)
                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} RequestToSpeak response: {result.Value.ResponseCase} for community {communityId}");
            }
        }

        public void PromoteToSpeakerInCurrentCall(string walletId)
        {
            if (string.IsNullOrEmpty(callId.Value)) return;
            if (status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL) return;

            cts = cts.SafeRestart();
            PromoteToSpeakerAsync(callId.Value).Forget();
            return;

            async UniTaskVoid PromoteToSpeakerAsync(string communityId)
            {
                Result<PromoteSpeakerInCommunityVoiceChatResponse> result = await voiceChatService.PromoteSpeakerInCommunityVoiceChatAsync(communityId, walletId, cts.Token)
                                                                                                    .SuppressToResultAsync(ReportCategory.COMMUNITY_VOICE_CHAT);

                if (cts.Token.IsCancellationRequested)
                    return;

                if (result.Success)
                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} PromoteToSpeaker response: {result.Value.ResponseCase} for community {communityId}, wallet {walletId}");
            }
        }

        public void DenySpeakerInCurrentCall(string walletId)
        {
            if (string.IsNullOrEmpty(callId.Value)) return;
            if (status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL) return;

            cts = cts.SafeRestart();
            DenySpeakerAsync(callId.Value, cts.Token).Forget();

            return;

            async UniTaskVoid DenySpeakerAsync(string communityId, CancellationToken ct)
            {
                Result<RejectSpeakRequestInCommunityVoiceChatResponse> result = await voiceChatService.DenySpeakerInCommunityVoiceChatAsync(communityId, walletId, ct)
                                                                                                        .SuppressToResultAsync(ReportCategory.COMMUNITY_VOICE_CHAT);

                if (ct.IsCancellationRequested)
                    return;

                if (result.Success)
                {
                    switch (result.Value.ResponseCase)
                    {
                        case RejectSpeakRequestInCommunityVoiceChatResponse.ResponseOneofCase.Ok:
                            break;
                    }
                }
            }
        }

        public void DemoteFromSpeakerInCurrentCall(string walletId)
        {
            if (string.IsNullOrEmpty(callId.Value)) return;
            if (status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL) return;

            cts = cts.SafeRestart();
            DemoteFromSpeakerAsync(callId.Value, cts.Token).Forget();
            return;

            async UniTaskVoid DemoteFromSpeakerAsync(string communityId, CancellationToken ct)
            {
                Result<DemoteSpeakerInCommunityVoiceChatResponse> result = await voiceChatService.DemoteSpeakerInCommunityVoiceChatAsync(communityId, walletId, ct)
                                                                                                    .SuppressToResultAsync(ReportCategory.COMMUNITY_VOICE_CHAT);

                if (ct.IsCancellationRequested)
                    return;

                if (result.Success)
                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} DemoteFromSpeaker response: {result.Value.ResponseCase} for community {communityId}, wallet {walletId}");
            }
        }

        public void KickPlayerFromCurrentCall(string walletId)
        {
            if (string.IsNullOrEmpty(callId.Value)) return;
            if (status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL) return;

            cts = cts.SafeRestart();
            KickPlayerAsync(callId.Value, cts.Token).Forget();
            return;

            async UniTaskVoid KickPlayerAsync(string communityId, CancellationToken ct)
            {
                Result<KickPlayerFromCommunityVoiceChatResponse> result = await voiceChatService.KickPlayerFromCommunityVoiceChatAsync(communityId, walletId, ct)
                                                                                                    .SuppressToResultAsync(ReportCategory.COMMUNITY_VOICE_CHAT);

                if (ct.IsCancellationRequested)
                    return;

                if (result.Success)
                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} KickPlayer response: {result.Value.ResponseCase} for community {communityId}, wallet {walletId}");
            }
        }

        public void MuteSpeakerInCurrentCall(string walletId, bool muted)
        {
            if (string.IsNullOrEmpty(callId.Value)) return;
            if (status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL) return;

            cts = cts.SafeRestart();
            MuteSpeakerAsync(callId.Value, cts.Token).Forget();
            return;

            async UniTaskVoid MuteSpeakerAsync(string communityId, CancellationToken ct)
            {
                Result<MuteSpeakerFromCommunityVoiceChatResponse> result = await voiceChatService.MuteSpeakerFromCommunityVoiceChatAsync(communityId, walletId, muted, ct)
                                                                                                    .SuppressToResultAsync(ReportCategory.COMMUNITY_VOICE_CHAT);

                if (ct.IsCancellationRequested)
                    return;

                if (result.Success)
                {
                    string action = muted ? "mute" : "unmute";
                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} MuteSpeaker response: {result.Value.ResponseCase} for community {communityId}, wallet {walletId}, action: {action}");
                }
            }
        }

        public void EndStreamInCurrentCall()
        {
            if (string.IsNullOrEmpty(callId.Value)) return;
            if (status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL) return;

            cts = cts.SafeRestart();
            EndStreamAsync(callId.Value, cts.Token).Forget();
            return;

            async UniTaskVoid EndStreamAsync(string communityId, CancellationToken ct)
            {
                Result<EndCommunityVoiceChatResponse> result = await voiceChatService.EndCommunityVoiceChatAsync(communityId, ct)
                                                                                    .SuppressToResultAsync(ReportCategory.COMMUNITY_VOICE_CHAT);

                if (ct.IsCancellationRequested)
                    return;

                if (result.Success)
                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} End stream response: {result.Value.ResponseCase} for community {communityId}");
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
            return;

            async UniTaskVoid UpdateStatusAsync()
            {
                await UniTask.SwitchToMainThread();
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} New status is {newStatus}");
                status.Value = newStatus;
            }
        }

        public void ResetVoiceChatData()
        {
            SetCallId(string.Empty);
            locallyStartedCommunityId = null;
        }

        public void SetCallId(string newCallId)
        {
            callId.Value = newCallId;
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
                    existingData.UpdateValue(false);

                // Clear locally started community ID if this was the one we started
                if (communityUpdate.CommunityId == locallyStartedCommunityId)
                    locallyStartedCommunityId = null;

                // Delegate scene unregistration to the tracker
                voiceChatSceneTrackerService.UnregisterCommunityFromScene(communityUpdate.CommunityId);
                voiceChatSceneTrackerService.RemoveActiveCommunityVoiceChat(communityUpdate.CommunityId);

                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Community voice chat ended for {communityUpdate.CommunityId}");
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
                participantCount = 0,
                moderatorCount = 0,
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
                    NotificationsBusController.Instance.AddNotification(new CommunityVoiceChatStartedNotification(communityUpdate.CommunityName, communityUpdate.CommunityImage, communityUpdate.CommunityId));
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

        public bool HasActiveVoiceChatCall(string communityId) =>
            !string.IsNullOrEmpty(communityId) && activeCommunityVoiceChats.ContainsKey(communityId);

        public IReadonlyReactiveProperty<bool> CommunityConnectionUpdates(string communityId)
        {
            if (communityVoiceChatCalls.TryGetValue(communityId, out ReactiveProperty<bool>? existingData)) { return existingData; }

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
