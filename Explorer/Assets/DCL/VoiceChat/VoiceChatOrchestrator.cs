using DCL.Diagnostics;
using DCL.Utilities;
using DCL.VoiceChat.Services;
using DCL.Web3;
using Decentraland.SocialService.V2;
using System;

namespace DCL.VoiceChat
{
    public enum CurrentVoiceChatType
    {
        NONE,
        PRIVATE,
        COMMUNITY
    }

    public class VoiceChatOrchestrator : IDisposable
    {
        private readonly PrivateVoiceChatController privateVoiceChatController;
        private readonly CommunitiesVoiceChatController communitiesVoiceChatController;
        private readonly VoiceChatEventBus voiceChatEventBus;
        private readonly IVoiceChatCallStatusService privateVoiceChatCallStatusService;
        private readonly IVoiceService rpcPrivateVoiceChatService;

        private readonly IDisposable statusSubscription;

        //Public access properties for UI that doesnt subscribe to updates.
        public CurrentVoiceChatType CurrentVoiceChatType => currentVoiceChatType;
        public VoiceChatStatus CurrentPrivateVoiceChatStatus => privateVoiceChatCallStatusService.Status.Value;


        private CurrentVoiceChatType currentVoiceChatType = CurrentVoiceChatType.NONE;

        public VoiceChatOrchestrator(
            PrivateVoiceChatController privateVoiceChatController,
            CommunitiesVoiceChatController communitiesVoiceChatController,
            VoiceChatEventBus voiceChatEventBus,
            IVoiceChatCallStatusService privateVoiceChatCallStatusService,
            IVoiceService rpcPrivateVoiceChatService)
        {
            this.privateVoiceChatController = privateVoiceChatController;
            this.communitiesVoiceChatController = communitiesVoiceChatController;
            this.voiceChatEventBus = voiceChatEventBus;
            this.privateVoiceChatCallStatusService = privateVoiceChatCallStatusService;
            this.rpcPrivateVoiceChatService = rpcPrivateVoiceChatService;

            voiceChatEventBus.StartPrivateVoiceChatRequested += OnStartVoiceChatRequested;
            rpcPrivateVoiceChatService.PrivateVoiceChatUpdateReceived += OnPrivateVoiceChatUpdateReceived;
            statusSubscription = privateVoiceChatCallStatusService.Status.Subscribe(OnPrivateVoiceChatStatusChanged);
        }

        private void OnPrivateVoiceChatUpdateReceived(PrivateVoiceChatUpdate update)
        {
            if (currentVoiceChatType != CurrentVoiceChatType.COMMUNITY)
            {
                privateVoiceChatCallStatusService.OnPrivateVoiceChatUpdateReceived(update);
            }
        }

        private void OnPrivateVoiceChatStatusChanged(VoiceChatStatus status)
        {
            //This logic needs to be improved.
            if (status == VoiceChatStatus.DISCONNECTED || status == VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR)
            {
                currentVoiceChatType = CurrentVoiceChatType.NONE;
            }
            else
            {
                currentVoiceChatType = CurrentVoiceChatType.PRIVATE;
            }
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"Switched Orchestrator state to {currentVoiceChatType}");
        }

        private void OnStartVoiceChatRequested(Web3Address walletId)
        {
            if (currentVoiceChatType != CurrentVoiceChatType.COMMUNITY)
            {
                privateVoiceChatCallStatusService.StartCall(walletId);
            }
            else
            {
                //Cant start a call as we are already in a community call. Show proper message?
            }
        }

        public void Dispose()
        {
        }
    }
}
