using Cysharp.Threading.Tasks;
using DCL.Chat.ChatServices;
using DCL.Chat.History;
using System.Threading;
using Utility.Types;

namespace DCL.Chat.ChatCommands
{
    public class ResolveInputStateCommand
    {
        private readonly GetUserChatStatusCommand getUserChatStatusCommand;
        private readonly CurrentChannelService currentChannelService;

        public ResolveInputStateCommand(GetUserChatStatusCommand getUserChatStatusCommand, CurrentChannelService currentChannelService)
        {
            this.getUserChatStatusCommand = getUserChatStatusCommand;
            this.currentChannelService = currentChannelService;
        }

        public async UniTask<Result<ChatUserStateService.ChatUserState>> ExecuteAsync(CancellationToken ct)
        {
            ChatChannel? currentChannel = currentChannelService.CurrentChannel;

            if (currentChannel == null)
                return currentChannelService.InputState = Result<ChatUserStateService.ChatUserState>.ErrorResult("No channel selected");

            switch (currentChannel.ChannelType)
            {
                case ChatChannel.ChatChannelType.NEARBY:
                case ChatChannel.ChatChannelType.COMMUNITY:
                    return currentChannelService.InputState = Result<ChatUserStateService.ChatUserState>.SuccessResult(ChatUserStateService.ChatUserState.CONNECTED);
                case ChatChannel.ChatChannelType.USER:
                {
                    ChatUserStateService.ChatUserState status = await getUserChatStatusCommand.ExecuteAsync(currentChannel.Id.Id, ct);

                    return currentChannelService.InputState = ct.IsCancellationRequested
                        ? Result<ChatUserStateService.ChatUserState>.CancelledResult()
                        : Result<ChatUserStateService.ChatUserState>.SuccessResult(status);
                }
                default:
                    return currentChannelService.InputState = Result<ChatUserStateService.ChatUserState>.ErrorResult($"{currentChannel.ChannelType} is not supported for input state resolution");
            }
        }
    }
}
