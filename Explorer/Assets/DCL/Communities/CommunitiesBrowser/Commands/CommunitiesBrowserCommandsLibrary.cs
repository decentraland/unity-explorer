using DCL.Chat.EventBus;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.VoiceChat;
using MVC;

namespace DCL.Communities.CommunitiesBrowser.Commands
{
    public class CommunitiesBrowserCommandsLibrary
    {
        public readonly JoinStreamCommand JoinStreamCommand;
        public readonly GoToStreamCommand GoToStreamCommand;
        public readonly CreateCommunityCommand CreateCommunityCommand;
        public readonly JoinCommunityCommand JoinCommunityCommand;

        public CommunitiesBrowserCommandsLibrary(
            ICommunityCallOrchestrator orchestrator,
            IChatEventBus chatEventBus,
            ISelfProfile selfProfile,
            INftNamesProvider nftNamesProvider,
            IMVCManager mvcManager,
            ISpriteCache spriteCache,
            CommunitiesDataProvider.CommunitiesDataProvider dataProvider
            )
        {
            JoinStreamCommand = new JoinStreamCommand(orchestrator, chatEventBus, mvcManager);
            GoToStreamCommand = new GoToStreamCommand(chatEventBus, mvcManager);
            CreateCommunityCommand = new CreateCommunityCommand(selfProfile, nftNamesProvider, mvcManager, spriteCache);
            JoinCommunityCommand = new JoinCommunityCommand(dataProvider);
        }
    }
}
