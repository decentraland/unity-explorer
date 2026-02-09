using DCL.Chat;
using DCL.Chat.ChatServices;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.SocialService;
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
        private readonly IPrivateVoiceChatCallStatusService privateVoiceChatCallStatusService;
        private readonly VoiceChatParticipantsStateService participantsStateService;
        private readonly SceneVoiceChatTrackerService sceneVoiceChatTrackerService;
        private readonly ICommunityVoiceChatCallStatusService communityVoiceChatCallStatusService;

        public readonly VoiceChatOrchestrator VoiceChatOrchestrator;

        public VoiceChatContainer(
            IRPCSocialServices socialServiceRPC,
            ISocialServiceEventBus socialServiceEventBus,
            IRoomHub roomHub,
            IWeb3IdentityCache identityCache,
            IWebRequestController webRequestController,
            IScenesCache scenesCache,
            IRealmNavigator realmNavigator,
            IRealmData realmData,
            IDecentralandUrlsSource urlsSource,
            ChatEventBus chatEventBus,
            CurrentChannelService currentChannelService)
        {
            rpcPrivateVoiceChatService = new RPCPrivateVoiceChatService(socialServiceRPC, socialServiceEventBus, identityCache);
            privateVoiceChatCallStatusService = FeaturesRegistry.Instance.IsEnabled(FeatureId.VOICE_CHAT)
                ? new PrivateVoiceChatCallStatusService(rpcPrivateVoiceChatService) : new PrivateVoiceChatCallStatusServiceNull();

            participantsStateService = new VoiceChatParticipantsStateService(roomHub.VoiceChatRoom().Room(), identityCache);

            rpcCommunityVoiceChatService = new RPCCommunityVoiceChatService(socialServiceRPC, socialServiceEventBus, webRequestController, urlsSource, identityCache);
            sceneVoiceChatTrackerService = new SceneVoiceChatTrackerService(scenesCache, realmNavigator, realmData);
            communityVoiceChatCallStatusService = FeaturesRegistry.Instance.IsEnabled(FeatureId.COMMUNITY_VOICE_CHAT)
                ? new CommunityVoiceChatCallStatusService(rpcCommunityVoiceChatService, sceneVoiceChatTrackerService) : new CommunityVoiceChatCallStatusServiceNull();
            VoiceChatOrchestrator = new VoiceChatOrchestrator(
                privateVoiceChatCallStatusService,
                communityVoiceChatCallStatusService,
                participantsStateService,
                sceneVoiceChatTrackerService,
                chatEventBus,
                currentChannelService);
        }

        public void Dispose()
        {
            privateVoiceChatCallStatusService.Dispose();
            communityVoiceChatCallStatusService.Dispose();
            rpcPrivateVoiceChatService.Dispose();
            VoiceChatOrchestrator.Dispose();
            rpcCommunityVoiceChatService.Dispose();
            participantsStateService.Dispose();
            sceneVoiceChatTrackerService.Dispose();
        }
    }
}
