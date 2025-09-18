using Cysharp.Threading.Tasks;
using DCL.Chat.ChatServices;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.VoiceChat;
using System.Threading;

namespace DCL.Chat.ChatCommands
{
    public class GetUserCallStatusCommand
    {
        private readonly PrivateConversationUserStateService userStateService;

        public GetUserCallStatusCommand(PrivateConversationUserStateService userStateService)
        {
            this.userStateService = userStateService;
        }

        public async UniTask<CallButtonPresenter.OtherUserCallStatus> ExecuteAsync(string userId, CancellationToken ct)
        {
            var result = await userStateService.GetChatUserStateAsync(userId, ct)
                                               .SuppressCancellationThrow()
                                               .SuppressToResultAsync(ReportCategory.CHAT_MESSAGES);

            switch (result.Value.Result)
            {
                case PrivateConversationUserStateService.ChatUserState.CONNECTED:
                    return CallButtonPresenter.OtherUserCallStatus.USER_AVAILABLE;
                case PrivateConversationUserStateService.ChatUserState.PRIVATE_MESSAGES_BLOCKED_BY_OWN_USER:
                    return CallButtonPresenter.OtherUserCallStatus.OWN_USER_REJECTS_CALLS;
                case PrivateConversationUserStateService.ChatUserState.PRIVATE_MESSAGES_BLOCKED:
                    return CallButtonPresenter.OtherUserCallStatus.USER_REJECTS_CALLS;
                case PrivateConversationUserStateService.ChatUserState.BLOCKED_BY_OWN_USER:
                case PrivateConversationUserStateService.ChatUserState.DISCONNECTED:
                default: return CallButtonPresenter.OtherUserCallStatus.USER_OFFLINE;
            }
        }
    }
}
