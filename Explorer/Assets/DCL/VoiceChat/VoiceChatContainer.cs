using DCL.Multiplayer.Connections.RoomHubs;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.SocialService;
using DCL.Utilities;
using DCL.VoiceChat.Services;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
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

        // Expose the community voice chat service for scene change system
        public CommunityVoiceChatCallStatusService CommunityVoiceChatCallStatusService => communityVoiceChatCallStatusService;

        public VoiceChatContainer(
            IRPCSocialServices socialServiceRPC,
            ISocialServiceEventBus socialServiceEventBus,
            IRoomHub roomHub,
            IWeb3IdentityCache identityCache,
            INotificationsBusController notificationsBusController,
            IWebRequestController webRequestController,
            PlayerParcelTrackerService parcelTrackerService,
            IRealmNavigator realmNavigator,
            IRealmData realmData)
        {
            rpcPrivateVoiceChatService = new RPCPrivateVoiceChatService(socialServiceRPC, socialServiceEventBus);
            rpcCommunityVoiceChatService = new RPCCommunityVoiceChatService(socialServiceRPC, socialServiceEventBus, webRequestController);
            participantsStateService = new VoiceChatParticipantsStateService(roomHub.VoiceChatRoom().Room(), identityCache);
            communityVoiceChatCallStatusService = new CommunityVoiceChatCallStatusService(rpcCommunityVoiceChatService, notificationsBusController, parcelTrackerService, realmNavigator, realmData);
            privateVoiceChatCallStatusService = new PrivateVoiceChatCallStatusService(rpcPrivateVoiceChatService);

            VoiceChatOrchestrator = new VoiceChatOrchestrator(
                privateVoiceChatCallStatusService,
                communityVoiceChatCallStatusService,
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
