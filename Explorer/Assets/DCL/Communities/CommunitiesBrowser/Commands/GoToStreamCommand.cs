using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.EventBus;
using DCL.UI.SharedSpaceManager;

namespace DCL.Communities.CommunitiesBrowser.Commands
{
    public class GoToStreamCommand
    {
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly IChatEventBus chatEventBus;

        public GoToStreamCommand(
            ISharedSpaceManager sharedSpaceManager,
            IChatEventBus chatEventBus)
        {
            this.sharedSpaceManager = sharedSpaceManager;
            this.chatEventBus = chatEventBus;
        }

        /// <summary>
        /// Focuses the Currently Active Community Voice Chat
        /// </summary>
        /// <param name="communityId">The community ID to join</param>
        public void Execute(string communityId)
        {
            GoToStreamAsync().Forget();
            return;

            async UniTaskVoid GoToStreamAsync()
            {
                await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatMainSharedAreaControllerShowParams(true));
                chatEventBus.OpenCommunityConversationUsingCommunityId(communityId);
            }
        }
    }
}
