using DCL.Chat;
using MVC;

namespace DCL.Communities.CommunitiesBrowser.Commands
{
    public class GoToStreamCommand
    {
        private readonly ChatEventBus chatEventBus;
        private readonly IMVCManager mvcManager;

        public GoToStreamCommand(ChatEventBus chatEventBus, IMVCManager mvcManager)
        {
            this.chatEventBus = chatEventBus;
            this.mvcManager = mvcManager;
        }

        /// <summary>
        /// Focuses the Currently Active Community Voice Chat
        /// </summary>
        /// <param name="communityId">The community ID to join</param>
        public void Execute(string communityId)
        {
            mvcManager.CloseAllNonPersistentControllers();
            chatEventBus.RaiseFocusRequestedEvent();
            chatEventBus.RaiseOpenCommunityConversationRequestedEvent(communityId);
        }
    }
}
