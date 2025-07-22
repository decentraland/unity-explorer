using DCL.Multiplayer.Connections.RoomHubs;
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
        private readonly VoiceChatParticipantsStateService participantsStateService;
        public readonly VoiceChatOrchestrator VoiceChatOrchestrator;

        public VoiceChatContainer(
            IRPCSocialServices socialServiceRPC,
            ISocialServiceEventBus socialServiceEventBus,
            IRoomHub roomHub)
        {
            rpcPrivateVoiceChatService = new RPCPrivateVoiceChatService(socialServiceRPC, socialServiceEventBus);
            rpcCommunityVoiceChatService = new RPCCommunityVoiceChatService(socialServiceRPC, socialServiceEventBus);
            privateVoiceChatCallStatusService = new PrivateVoiceChatCallStatusService(rpcPrivateVoiceChatService);
            communityVoiceChatCallStatusService = new CommunityVoiceChatCallStatusService(rpcCommunityVoiceChatService);
            participantsStateService = new VoiceChatParticipantsStateService(roomHub.VoiceChatRoom().Room());

            VoiceChatOrchestrator = new VoiceChatOrchestrator(
                privateVoiceChatCallStatusService,
                communityVoiceChatCallStatusService,
                rpcPrivateVoiceChatService,
                rpcCommunityVoiceChatService,
                participantsStateService);
        }

        public void Dispose()
        {
            privateVoiceChatCallStatusService?.Dispose();
            communityVoiceChatCallStatusService?.Dispose();
            rpcPrivateVoiceChatService?.Dispose();
            VoiceChatOrchestrator?.Dispose();
            rpcCommunityVoiceChatService?.Dispose();
            participantsStateService?.Dispose();
        }
    }
}
