using Cysharp.Threading.Tasks;
using DCL.Chat.ChatServices;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using System.Threading;
using Utility;

namespace DCL.Chat.ChatCommands
{
    public class GetUserChatStatusCommand
    {
        private readonly IEventBus eventBus;
        private readonly PrivateConversationUserStateService userStateService;

        public GetUserChatStatusCommand(PrivateConversationUserStateService userStateService, IEventBus eventBus)
        {
            this.eventBus = eventBus;
            this.userStateService = userStateService;
        }

        public async UniTask<PrivateConversationUserStateService.ChatUserState> ExecuteAsync(string userId, CancellationToken ct)
        {
            var result = await userStateService.GetChatUserStateAsync(userId, ct)
                                               .SuppressCancellationThrow()
                                               .SuppressToResultAsync(ReportCategory.CHAT_MESSAGES);

            if (ct.IsCancellationRequested || !result.Success)
            {
                eventBus.Publish(new ChatEvents.UserStatusUpdatedEvent(new ChatChannel.ChannelId(userId), userId, false));
                return PrivateConversationUserStateService.ChatUserState.DISCONNECTED;
            }

            bool isOnline = result.Value.Result == PrivateConversationUserStateService.ChatUserState.CONNECTED;
            eventBus.Publish(new ChatEvents.UserStatusUpdatedEvent(new ChatChannel.ChannelId(userId), userId, isOnline));

            return result.Value.Result;
        }
    }
}
