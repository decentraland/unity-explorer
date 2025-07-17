using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.VoiceChat.Services;
using DCL.Web3;
using Decentraland.SocialService.V2;
using System;
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
        public CommunityVoiceChatCallStatusService(ICommunityVoiceService voiceChatService)
        {
            this.voiceChatService = voiceChatService;
            cts = new CancellationTokenSource();
        }

        public override void StartCall(string communityId)
        {
            //We can start a call only if we are not connected or trying to start a call
            if (Status.Value is not VoiceChatStatus.DISCONNECTED and not VoiceChatStatus.VOICE_CHAT_USER_BUSY and not VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR) return;

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
                        UpdateStatus(VoiceChatStatus.VOICE_CHAT_STARTED_CALL);
                        break;

                    case StartCommunityVoiceChatResponse.ResponseOneofCase.InvalidRequest:
                    case StartCommunityVoiceChatResponse.ResponseOneofCase.ConflictingError:
                        //Do we want to do something specific here?
                        ResetVoiceChatData();
                        UpdateStatus(VoiceChatStatus.VOICE_CHAT_USER_BUSY);
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
            ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, "Community voice chat HangUp not yet implemented");
        }

        public override void HandleLivekitConnectionFailed()
        {
            ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, "Community voice chat HandleLivekitConnectionFailed not yet implemented");
            UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
        }

        public void OnPrivateVoiceChatUpdateReceived(CommunityVoiceChatUpdate update)
        {

        }
    }
}
