using DCL.Diagnostics;
using DCL.Utilities;
using DCL.Web3;
using System;

namespace DCL.VoiceChat
{
    public class VoiceChatOrchestrator : IDisposable
    {
        private readonly PrivateVoiceChatController privateVoiceChatController;
        private readonly CommunitiesVoiceChatController communitiesVoiceChatController;
        private readonly VoiceChatEventBus voiceChatEventBus;
        private readonly IVoiceChatCallStatusService voiceChatCallStatusService;

        private readonly IDisposable statusSubscription;

        private enum CurrentVoiceChat
        {
            NONE,
            PRIVATE,
            COMMUNITY
        }

        private CurrentVoiceChat currentVoiceChat = CurrentVoiceChat.NONE;

        public VoiceChatOrchestrator(
            PrivateVoiceChatController privateVoiceChatController,
            CommunitiesVoiceChatController communitiesVoiceChatController,
            VoiceChatEventBus voiceChatEventBus,
            IVoiceChatCallStatusService voiceChatCallStatusService)
        {
            this.privateVoiceChatController = privateVoiceChatController;
            this.communitiesVoiceChatController = communitiesVoiceChatController;
            this.voiceChatEventBus = voiceChatEventBus;
            this.voiceChatCallStatusService = voiceChatCallStatusService;


            voiceChatEventBus.StartPrivateVoiceChatRequested += OnStartVoiceChatRequested;
            statusSubscription = voiceChatCallStatusService.Status.Subscribe(OnPrivateVoiceChatStatusChanged);
        }

        private void OnPrivateVoiceChatStatusChanged(VoiceChatStatus status)
        {
            //This logic needs to be improved.
            if (status == VoiceChatStatus.DISCONNECTED || status == VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR)
            {
                currentVoiceChat = CurrentVoiceChat.NONE;
            }
            else
            {
                currentVoiceChat = CurrentVoiceChat.PRIVATE;
            }
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"Switched Orchestrator state to {currentVoiceChat}");
        }

        private void OnStartVoiceChatRequested(Web3Address walletId)
        {
            if (currentVoiceChat != CurrentVoiceChat.COMMUNITY)
            {
                voiceChatCallStatusService.StartCall(walletId);
            }
        }

        public void Dispose()
        {
        }
    }
}
