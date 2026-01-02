using Cysharp.Threading.Tasks;
using DCL.Chat.ChatServices;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using System.Threading;

namespace DCL.Chat.ChatCommands
{
    public class GetUserChatStatusCommand
    {
        private readonly ChatEventBus eventBus;
        private readonly PrivateConversationUserStateService userStateService;

        public GetUserChatStatusCommand(PrivateConversationUserStateService userStateService, ChatEventBus eventBus)
        {
            this.eventBus = eventBus;
            this.userStateService = userStateService;
        }

        public async UniTask<PrivateConversationUserStateService.UserState> ExecuteAsync(string userId, CancellationToken ct)
        {
            var result = await userStateService.GetChatUserStateAsync(userId, ct)
                                               .SuppressCancellationThrow()
                                               .SuppressToResultAsync(ReportCategory.CHAT_MESSAGES);

            if (ct.IsCancellationRequested || !result.Success)
            {
                eventBus.RaiseUserStatusUpdatedEvent(new ChatChannel.ChannelId(userId), ChatChannel.ChatChannelType.USER, userId, false);
                return new PrivateConversationUserStateService.UserState(false, PrivateConversationUserStateService.ChatUserState.DISCONNECTED);
            }

            bool isOnline = result.Value.Result.IsConsideredOnline;
            eventBus.RaiseUserStatusUpdatedEvent(new ChatChannel.ChannelId(userId), ChatChannel.ChatChannelType.USER, userId, isOnline);

            return result.Value.Result;
        }
    }
}
