using DCL.SocialService;
using DCL.VoiceChat.Services;
using System;

namespace DCL.VoiceChat
{
    public class VoiceChatContainer : IDisposable
    {
        private readonly IVoiceService rpcPrivateVoiceChatService;
        private readonly ICommunityVoiceService rpcCommunityVoiceChatService;
        private readonly PrivateVoiceChatCallStatusService privateVoiceChatCallStatusService;
        private readonly CommunityVoiceChatCallStatusService communityVoiceChatCallStatusService;
        public readonly VoiceChatOrchestrator VoiceChatOrchestrator;

        public VoiceChatContainer(
            IRPCSocialServices socialServiceRPC,
            ISocialServiceEventBus socialServiceEventBus)
        {
            rpcPrivateVoiceChatService = new RPCPrivateVoiceChatService(socialServiceRPC, socialServiceEventBus);
            rpcCommunityVoiceChatService = new RPCCommunityVoiceChatService(socialServiceRPC);
            privateVoiceChatCallStatusService = new PrivateVoiceChatCallStatusService(rpcPrivateVoiceChatService);
            communityVoiceChatCallStatusService = new CommunityVoiceChatCallStatusService(rpcCommunityVoiceChatService);
            VoiceChatOrchestrator = new VoiceChatOrchestrator(privateVoiceChatCallStatusService, communityVoiceChatCallStatusService, rpcPrivateVoiceChatService, rpcCommunityVoiceChatService);
        }

        public void Dispose()
        {
            privateVoiceChatCallStatusService?.Dispose();
            communityVoiceChatCallStatusService?.Dispose();
            rpcPrivateVoiceChatService?.Dispose();
            VoiceChatOrchestrator?.Dispose();
            rpcCommunityVoiceChatService?.Dispose();
        }
    }
}
