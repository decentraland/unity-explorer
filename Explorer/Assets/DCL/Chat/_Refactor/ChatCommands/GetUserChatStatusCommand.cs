using Cysharp.Threading.Tasks;
using DCL.Chat.ChatServices;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using System.Threading;
using Utility;

namespace DCL.Chat.ChatCommands
{
    public class GetUserChatStatusCommand
    {
        private readonly IEventBus eventBus;
        private readonly ChatUserStateService userStateService;

        public GetUserChatStatusCommand(ChatUserStateService userStateService, IEventBus eventBus)
        {
            this.eventBus = eventBus;
            this.userStateService = userStateService;
        }

        public async UniTask<ChatUserStateService.ChatUserState> ExecuteAsync(string userId, CancellationToken ct)
        {
            var result = await userStateService.GetChatUserStateAsync(userId, ct)
                                               .SuppressCancellationThrow()
                                               .SuppressToResultAsync(ReportCategory.CHAT_MESSAGES);

            if (ct.IsCancellationRequested || !result.Success)
            {
                eventBus.Publish(new ChatEvents.UserStatusUpdatedEvent { UserId = userId, IsOnline = false });
                return ChatUserStateService.ChatUserState.DISCONNECTED;
            }

            bool isOnline = result.Value.Result == ChatUserStateService.ChatUserState.CONNECTED;
            eventBus.Publish(new ChatEvents.UserStatusUpdatedEvent { UserId = userId, IsOnline = isOnline });

            return result.Value.Result;
        }
    }
}
