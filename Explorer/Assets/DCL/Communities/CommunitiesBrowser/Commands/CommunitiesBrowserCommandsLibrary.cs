using DCL.Chat.EventBus;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.UI.SharedSpaceManager;
using MVC;

#if !NO_LIVEKIT_MODE
using DCL.VoiceChat;
#endif

namespace DCL.Communities.CommunitiesBrowser.Commands
{
    public class CommunitiesBrowserCommandsLibrary
    {

#if !NO_LIVEKIT_MODE
        public readonly JoinStreamCommand JoinStreamCommand;
        public readonly GoToStreamCommand GoToStreamCommand;
        public readonly CreateCommunityCommand CreateCommunityCommand;
#endif
        public readonly JoinCommunityCommand JoinCommunityCommand;

        public CommunitiesBrowserCommandsLibrary(

#if !NO_LIVEKIT_MODE
            ICommunityCallOrchestrator orchestrator,
#endif

            ISharedSpaceManager sharedSpaceManager,

#if !NO_LIVEKIT_MODE
            IChatEventBus chatEventBus,
#endif

            ISelfProfile selfProfile,
            INftNamesProvider nftNamesProvider,
            IMVCManager mvcManager,
            ISpriteCache spriteCache,
            CommunitiesDataProvider.CommunitiesDataProvider dataProvider
            )
        {

#if !NO_LIVEKIT_MODE
            JoinStreamCommand = new JoinStreamCommand(orchestrator, sharedSpaceManager, chatEventBus);
            GoToStreamCommand = new GoToStreamCommand(sharedSpaceManager, chatEventBus);
            CreateCommunityCommand = new CreateCommunityCommand(selfProfile, nftNamesProvider, mvcManager, spriteCache);
#endif

            JoinCommunityCommand = new JoinCommunityCommand(dataProvider);
        }
    }
}
