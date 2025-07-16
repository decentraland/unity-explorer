using DCL.SocialService;
using DCL.VoiceChat.Services;
using System;

namespace DCL.VoiceChat
{
    public class VoiceChatContainer : IDisposable
    {
        private readonly IVoiceService rpcPrivateVoiceChatService;
        private readonly PrivateVoiceChatCallStatusService privateVoiceChatCallStatusService;
        private readonly CommunityVoiceChatCallStatusService communityVoiceChatCallStatusService;
        public readonly VoiceChatOrchestrator VoiceChatOrchestrator;

        public VoiceChatContainer(
            IRPCSocialServices socialServiceRPC,
            ISocialServiceEventBus socialServiceEventBus)
        {
            rpcPrivateVoiceChatService = new RPCPrivateVoiceChatService(socialServiceRPC, socialServiceEventBus);
            privateVoiceChatCallStatusService = new PrivateVoiceChatCallStatusService(rpcPrivateVoiceChatService);
            communityVoiceChatCallStatusService = new CommunityVoiceChatCallStatusService();
            VoiceChatOrchestrator = new VoiceChatOrchestrator(privateVoiceChatCallStatusService, communityVoiceChatCallStatusService, rpcPrivateVoiceChatService);
        }

        public void Dispose()
        {
            privateVoiceChatCallStatusService?.Dispose();
            communityVoiceChatCallStatusService?.Dispose();
            rpcPrivateVoiceChatService?.Dispose();
            VoiceChatOrchestrator?.Dispose();
        }
    }
}
