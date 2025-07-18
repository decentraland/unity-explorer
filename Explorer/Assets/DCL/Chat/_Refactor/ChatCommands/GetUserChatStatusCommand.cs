using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.EventBus;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using Utilities;
using Utility;

namespace DCL.Chat.ChatUseCases
{
    public class GetUserChatStatusCommand
    {
        private readonly IEventBus eventBus;
        private readonly ChatUserStateUpdater userStateUpdater;

        public GetUserChatStatusCommand(ChatUserStateUpdater userStateUpdater, IEventBus eventBus)
        {
            this.eventBus = eventBus;
            this.userStateUpdater = userStateUpdater;
        }

        public async UniTask<ChatUserStateUpdater.ChatUserState> ExecuteAsync(string userId, CancellationToken ct)
        {
            var result = await userStateUpdater.GetChatUserStateAsync(userId, ct)
                .SuppressCancellationThrow()
                .SuppressToResultAsync(ReportCategory.CHAT_MESSAGES);

            if (ct.IsCancellationRequested || !result.Success)
            {
                eventBus.Publish(new ChatEvents.UserStatusUpdatedEvent { UserId = userId, IsOnline = false });
                return ChatUserStateUpdater.ChatUserState.DISCONNECTED;
            }

            bool isOnline = result.Value.Result == ChatUserStateUpdater.ChatUserState.CONNECTED;
            eventBus.Publish(new ChatEvents.UserStatusUpdatedEvent { UserId = userId, IsOnline = isOnline });

            return result.Value.Result;
        }
    }
}
