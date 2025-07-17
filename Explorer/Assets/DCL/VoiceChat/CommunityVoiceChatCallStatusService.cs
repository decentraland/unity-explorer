using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.VoiceChat.Services;
using Decentraland.SocialService.V2;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Implementation of voice chat call status service for community calls.
    /// Currently provides empty implementations as community voice chat is not yet implemented.
    /// </summary>
    public class CommunityVoiceChatCallStatusService : VoiceChatCallStatusServiceBase, ICommunityVoiceChatCallStatusService
    {
        private readonly ICommunityVoiceService voiceChatService;
        private CancellationTokenSource cts;
        private readonly Dictionary<string, string> communityVoiceChatCalls = new();

        public CommunityVoiceChatCallStatusService(ICommunityVoiceService voiceChatService)
        {
            this.voiceChatService = voiceChatService;
            this.voiceChatService.CommunityVoiceChatUpdateReceived += OnCommunityVoiceChatUpdateReceived;

            SubscribeToCommunityVoiceChatUpdatesAsync(cts.Token).Forget();
            cts = new CancellationTokenSource();
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

                switch (response.ResponseCase)
                {
                    //When the call can be started
                    case StartCommunityVoiceChatResponse.ResponseOneofCase.Ok:
                        RoomUrl = response.Ok.Credentials.ConnectionUrl;
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
            catch (Exception e)
            {
            }
        }

        public override void HangUp()
        {
            //TODO: currently just exits, need to figure out how to handle hang up
            UpdateStatus(VoiceChatStatus.DISCONNECTED);
        }

        public void JoinCommunityVoiceChat()
        {
            //We can start a call only if we are not connected or trying to start a call
            if (Status.Value is not VoiceChatStatus.DISCONNECTED and not VoiceChatStatus.VOICE_CHAT_BUSY and not VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR) return;

            cts = cts.SafeRestart();
            UpdateStatus(VoiceChatStatus.VOICE_CHAT_STARTED_CALL);
            JoinCommunityVoiceChatAsync(CallId, cts.Token).Forget();
        }

        private async UniTaskVoid JoinCommunityVoiceChatAsync(string communityId, CancellationToken ct)
        {
            try
            {
                JoinCommunityVoiceChatResponse response = await voiceChatService.JoinCommunityVoiceChatAsync(communityId, ct);

                switch (response.ResponseCase)
                {
                    case JoinCommunityVoiceChatResponse.ResponseOneofCase.Ok:
                        RoomUrl = response.Ok.Credentials.ConnectionUrl;
                        UpdateStatus(VoiceChatStatus.VOICE_CHAT_IN_CALL);
                        break;
                    default:
                        ResetVoiceChatData();
                        UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
                        break;
                }
            }
            catch (Exception e)
            {
            }
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

            if (string.IsNullOrEmpty(communityUpdate.VoiceChatId))
            {
                // Remove community from dictionary if call_id is empty
                if (communityVoiceChatCalls.Remove(communityUpdate.CommunityId))
                {
                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"Removed community {communityUpdate.CommunityId} from voice chat calls");
                }
            }
            else
            {
                // Add or update community with new call_id
                communityVoiceChatCalls[communityUpdate.CommunityId] = communityUpdate.VoiceChatId;
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"Updated community {communityUpdate.CommunityId} with Voice Chat ID {communityUpdate.VoiceChatId}");
            }
        }

        private async UniTaskVoid SubscribeToCommunityVoiceChatUpdatesAsync(CancellationToken ct)
        {
            try
            {
                await voiceChatService.SubscribeToCommunityVoiceChatUpdatesAsync(ct)
                                           .SuppressToResultAsync(ReportCategory.COMMUNITY_VOICE_CHAT);
            }
            catch (OperationCanceledException) { } // Expected, don't report
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.COMMUNITY_VOICE_CHAT));
            }
        }

        public override void Dispose()
        {
            if (voiceChatService != null)
            {
                voiceChatService.CommunityVoiceChatUpdateReceived -= OnCommunityVoiceChatUpdateReceived;
                voiceChatService.Dispose();
            }

            cts.SafeCancelAndDispose();
            base.Dispose();
        }

        public bool HasActiveVoiceChatCall(string communityId, out string? callId)
        {
            if (string.IsNullOrEmpty(communityId))
            {
                callId = null;
                return false;
            }

            return communityVoiceChatCalls.TryGetValue(communityId, out callId);
        }
    }
}
