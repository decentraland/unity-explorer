using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.EventBus;
using DCL.UI.SharedSpaceManager;
using DCL.VoiceChat;

namespace DCL.Communities.CommunitiesBrowser.Commands
{
    /// <summary>
    /// Command to join a community voice chat stream.
    /// </summary>
    public class JoinStreamCommand
    {
        private const int UI_CLOSE_DELAY = 500;

        private readonly ICommunityCallOrchestrator orchestrator;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly IChatEventBus chatEventBus;

        public JoinStreamCommand(
            ICommunityCallOrchestrator orchestrator,
            ISharedSpaceManager sharedSpaceManager,
            IChatEventBus chatEventBus)
        {
            this.orchestrator = orchestrator;
            this.sharedSpaceManager = sharedSpaceManager;
            this.chatEventBus = chatEventBus;
        }

        /// <summary>
        /// Joins a community voice chat stream.
        /// </summary>
        /// <param name="communityId">The community ID to join</param>
        public void Execute(string communityId)
        {
            // If we already joined, we cannot join again
            if (orchestrator.CurrentCommunityId.Value == communityId)
                return;

            JoinStreamAsync().Forget();
            return;

            async UniTaskVoid JoinStreamAsync()
            {
                await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatMainSharedAreaControllerShowParams(true));
                chatEventBus.OpenCommunityConversationUsingCommunityId(communityId);
                // We wait until the panel has disappeared before starting the call, so the UX feels better.
                await UniTask.Delay(UI_CLOSE_DELAY);
                orchestrator.JoinCommunityVoiceChat(communityId, true);
            }
        }
    }
}
