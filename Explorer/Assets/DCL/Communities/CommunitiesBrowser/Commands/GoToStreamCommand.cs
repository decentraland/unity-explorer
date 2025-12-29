using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.EventBus;
using DCL.ChatArea;
using MVC;

namespace DCL.Communities.CommunitiesBrowser.Commands
{
    public class GoToStreamCommand
    {
        private readonly IChatEventBus chatEventBus;
        private readonly IMVCManager mvcManager;

        public GoToStreamCommand(IChatEventBus chatEventBus, IMVCManager mvcManager)
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
            GoToStreamAsync().Forget();
            return;

            async UniTaskVoid GoToStreamAsync()
            {
                await mvcManager.ShowAsync(ChatMainSharedAreaController.IssueCommand(new ChatMainSharedAreaControllerShowParams(true)));
                chatEventBus.OpenCommunityConversationUsingCommunityId(communityId);
            }
        }
    }
}
