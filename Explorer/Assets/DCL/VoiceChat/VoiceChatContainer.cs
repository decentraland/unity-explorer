using DCL.SocialService;
using DCL.VoiceChat.Services;
using System;

namespace DCL.VoiceChat
{
    public class VoiceChatContainer : IDisposable
    {
        public readonly IVoiceService RPCPrivateVoiceChatService;
        public readonly IVoiceChatCallStatusService VoiceChatCallStatusService;
        public readonly VoiceChatOrchestrator VoiceChatOrchestrator;

        public VoiceChatContainer(
            IRPCSocialServices socialServiceRPC,
            ISocialServiceEventBus socialServiceEventBus)
        {
            RPCPrivateVoiceChatService = new RPCPrivateVoiceChatService(socialServiceRPC, socialServiceEventBus);
            VoiceChatCallStatusService = new VoiceChatCallStatusService(RPCPrivateVoiceChatService);
            VoiceChatOrchestrator = new VoiceChatOrchestrator(VoiceChatCallStatusService, RPCPrivateVoiceChatService);
        }

        public void Dispose()
        {
            VoiceChatCallStatusService?.Dispose();
            RPCPrivateVoiceChatService?.Dispose();
            VoiceChatOrchestrator?.Dispose();
        }
    }
}
