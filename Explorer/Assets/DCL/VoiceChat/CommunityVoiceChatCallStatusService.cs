using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.VoiceChat.Services;
using DCL.Web3;
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
        private readonly ICommunityVoiceService communityVoiceService;
        private CancellationTokenSource cts;
        private readonly Dictionary<string, string> communityVoiceChatCalls = new();

        public CommunityVoiceChatCallStatusService(ICommunityVoiceService communityVoiceService)
        {
            this.communityVoiceService = communityVoiceService;
            this.communityVoiceService.CommunityVoiceChatUpdateReceived += OnCommunityVoiceChatUpdateReceived;
            cts = new CancellationTokenSource();

            SubscribeToCommunityVoiceChatUpdatesAsync(cts.Token).Forget();
        }

        public override void Dispose()
        {
            if (communityVoiceService != null)
            {
                communityVoiceService.CommunityVoiceChatUpdateReceived -= OnCommunityVoiceChatUpdateReceived;
                communityVoiceService.Dispose();
            }

            cts.SafeCancelAndDispose();
            base.Dispose();
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
                await communityVoiceService.SubscribeToCommunityVoiceChatUpdatesAsync(ct)
                    .SuppressToResultAsync(ReportCategory.COMMUNITY_VOICE_CHAT);
            }
            catch (OperationCanceledException) { } // Expected, don't report
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.COMMUNITY_VOICE_CHAT));
            }
        }

        public override void StartCall(Web3Address userAddress)
        {
            ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, "Community voice chat StartCall not yet implemented");
        }

        public override void HangUp()
        {
            ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, "Community voice chat HangUp not yet implemented");
        }

        public override void HandleLivekitConnectionFailed()
        {
            ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, "Community voice chat HandleLivekitConnectionFailed not yet implemented");
            UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
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
