using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.VoiceChat;
using MVC;

namespace DCL.Communities.CommunitiesBrowser.Commands
{
    /// <summary>
    /// Command to join a community voice chat stream.
    /// </summary>
    public class JoinStreamCommand
    {
        private const int UI_CLOSE_DELAY = 600;

        private readonly ICommunityCallOrchestrator orchestrator;
        private readonly ChatEventBus chatEventBus;
        private readonly IMVCManager mvcManager;

        public JoinStreamCommand(
            ICommunityCallOrchestrator orchestrator,
            ChatEventBus chatEventBus,
            IMVCManager mvcManager)
        {
            this.orchestrator = orchestrator;
            this.chatEventBus = chatEventBus;
            this.mvcManager = mvcManager;
        }

        /// <summary>
        /// Joins a community voice chat stream.
        /// </summary>
        /// <param name="communityId">The community ID to join</param>
        /// <param name="shouldOpenConversation">If we should open the conversation in the chat (false for communities we don't belong to)</param>
        public void Execute(string communityId, bool shouldOpenConversation)
        {
            // If we already joined, we cannot join again
            if (orchestrator.CurrentCommunityId.Value == communityId)
                return;

            JoinStreamAsync().Forget();
            return;

            async UniTaskVoid JoinStreamAsync()
            {
                mvcManager.CloseAllNonPersistentControllers();
                chatEventBus.RaiseFocusRequestedEvent();

                if (shouldOpenConversation)
                    chatEventBus.RaiseOpenCommunityConversationRequestedEvent(communityId);

                // We wait until the panel has disappeared before starting the call, so the UX feels better.
                await UniTask.Delay(UI_CLOSE_DELAY);
                orchestrator.JoinCommunityVoiceChat(communityId, true);
            }
        }
    }
}
