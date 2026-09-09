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

            // result.Value is default on cancellation or failure and must not be decoded as a user state
            if (ct.IsCancellationRequested || !result.Success)
                return CallButtonPresenter.OtherUserCallStatus.UserOffline;

            switch (result.Value.Result.ChatUserState)
            {
                case PrivateConversationUserStateService.ChatUserState.Connected:
                    return CallButtonPresenter.OtherUserCallStatus.UserAvailable;
                case PrivateConversationUserStateService.ChatUserState.PrivateMessagesBlockedByOwnUser:
                    return CallButtonPresenter.OtherUserCallStatus.OwnUserRejectsCalls;
                case PrivateConversationUserStateService.ChatUserState.PrivateMessagesBlocked:
                    return CallButtonPresenter.OtherUserCallStatus.UserRejectsCalls;
                case PrivateConversationUserStateService.ChatUserState.BlockedByOwnUser:
                case PrivateConversationUserStateService.ChatUserState.Disconnected:
                default: return CallButtonPresenter.OtherUserCallStatus.UserOffline;
            }
        }
    }
}
