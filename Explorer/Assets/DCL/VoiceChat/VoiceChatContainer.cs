using DCL.Multiplayer.Connections.RoomHubs;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.SocialService;
using DCL.Utilities;
using DCL.VoiceChat.Services;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.SceneLifeCycle.Realm;
using System;

namespace DCL.VoiceChat
{
    public class VoiceChatContainer : IDisposable
    {
        private readonly IVoiceService rpcPrivateVoiceChatService;
        private readonly ICommunityVoiceService rpcCommunityVoiceChatService;
        private readonly PrivateVoiceChatCallStatusService privateVoiceChatCallStatusService;
        private readonly VoiceChatParticipantsStateService participantsStateService;
        private readonly SceneVoiceChatTrackerService sceneVoiceChatTrackerService;

        public readonly CommunityVoiceChatCallStatusService CommunityVoiceChatCallStatusService;
        public readonly VoiceChatOrchestrator VoiceChatOrchestrator;

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
            privateVoiceChatCallStatusService = new PrivateVoiceChatCallStatusService(rpcPrivateVoiceChatService);

            participantsStateService = new VoiceChatParticipantsStateService(roomHub.VoiceChatRoom().Room(), identityCache);

            rpcCommunityVoiceChatService = new RPCCommunityVoiceChatService(socialServiceRPC, socialServiceEventBus, webRequestController);
            sceneVoiceChatTrackerService = new SceneVoiceChatTrackerService(parcelTrackerService, realmNavigator, realmData);
            CommunityVoiceChatCallStatusService = new CommunityVoiceChatCallStatusService(rpcCommunityVoiceChatService, notificationsBusController, sceneVoiceChatTrackerService);
            VoiceChatOrchestrator = new VoiceChatOrchestrator(privateVoiceChatCallStatusService, CommunityVoiceChatCallStatusService, participantsStateService, sceneVoiceChatTrackerService);
        }

        public void Dispose()
        {
            privateVoiceChatCallStatusService.Dispose();
            CommunityVoiceChatCallStatusService.Dispose();
            rpcPrivateVoiceChatService.Dispose();
            VoiceChatOrchestrator.Dispose();
            rpcCommunityVoiceChatService.Dispose();
            participantsStateService.Dispose();
            sceneVoiceChatTrackerService.Dispose();
        }
    }
}
