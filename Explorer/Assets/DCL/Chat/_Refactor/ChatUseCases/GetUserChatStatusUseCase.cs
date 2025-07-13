using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;

namespace DCL.Chat.ChatUseCases
{
    public class GetUserChatStatusUseCase
    {
        private readonly ChatUserStateUpdater userStateUpdater;

        public GetUserChatStatusUseCase(ChatUserStateUpdater userStateUpdater)
        {
            this.userStateUpdater = userStateUpdater;
        }

        public async UniTask<ChatUserStateUpdater.ChatUserState> ExecuteAsync(string userId, CancellationToken ct)
        {
            var result = await userStateUpdater.GetChatUserStateAsync(userId, ct)
                .SuppressCancellationThrow()
                .SuppressToResultAsync(ReportCategory.CHAT_MESSAGES);

            if (ct.IsCancellationRequested || !result.Success)
                return ChatUserStateUpdater.ChatUserState.DISCONNECTED;

            return result.Value.Result;
        }
    }
}